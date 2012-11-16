/*
	Copyright (c) 2012, Florian Rappl.
	All rights reserved.

	Redistribution and use in source and binary forms, with or without
	modification, are permitted provided that the following conditions are met:
		* Redistributions of source code must retain the above copyright
		  notice, this list of conditions and the following disclaimer.
		* Redistributions in binary form must reproduce the above copyright
		  notice, this list of conditions and the following disclaimer in the
		  documentation and/or other materials provided with the distribution.
		* Neither the name of the YAMP team nor the names of its contributors
		  may be used to endorse or promote products derived from this
		  software without specific prior written permission.

	THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
	ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
	DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
	(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
	LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
	ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
	(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
	SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace YAMP
{
	/// <summary>
	/// The class used for the global and local ParseTree
	/// </summary>
	public class ParseTree
	{
		#region Members

		Operator _operator;
		Expression[] _expressions;
		string _input;
		int _offset;
		QueryContext _query;
		char _lastSkip;

		#endregion

		#region ctor

		internal ParseTree(Operator op, Expression[] exps)
		{
			_operator = op;
			_expressions = exps;
		}

		internal ParseTree(Operator op, Expression exp)
		{
			_operator = op;
			_expressions = new Expression[] { exp };
		}

		internal ParseTree(Operator op, Expression left, Expression right)
		{
			_operator = op;
			_expressions = new Expression[] { left, right };
		}

		internal ParseTree(QueryContext query, string input, int offset)
		{
			_query = query;
			_offset = offset;
			_input = input;
			Parse();
		}

		/// <summary>
		/// Main constructor - use as entry point!
		/// </summary>
		/// <param name="query">The query context.</param>
		/// <param name="input">The input to parse.</param>
		public ParseTree(QueryContext query, string input) : this(query, input, 0)
		{
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the operator used for this parse tree (can be null).
		/// </summary>
		public Operator Operator
		{
			get
			{
				return _operator;
			}
			set
			{
				_operator = value;
			}
		}

		/// <summary>
		/// Gets the array with all found expressions in the parse tree.
		/// </summary>
		public Expression[] Expressions
		{
			get
			{
				return _expressions;
			}
			set
			{
				_expressions = value;
			}
		}

		/// <summary>
		/// Gets a value if the last output was actually saved in a variable.
		/// </summary>
		public bool IsAssignment
		{
			get
			{
				if (Operator != null)
					return Operator is AssignmentOperator;

				if (Expressions.Length != 1)
					return false;

				if (Expressions[0] is TreeExpression)
					return (Expressions[0] as TreeExpression).Tree.IsAssignment;

				return false;
			}
		}

		/// <summary>
		/// Gets a value if the interpreter has any content (can do interpretation).
		/// </summary>
		public bool HasContent
		{
			get
			{
				return _expressions != null && _expressions.Length > 0;
			}
		}

		/// <summary>
		/// Gets the assigned QueryContext.
		/// </summary>
		public QueryContext Query
		{
			get { return _query; }
		}

		/// <summary>
		/// Gets the last skipped character.
		/// </summary>
		protected char LastSkip
		{
			get { return _lastSkip; }
		}

		#endregion

		#region Methods

		protected virtual Operator FindOperator(string input)
		{
			return Tokens.Instance.FindOperator(_query, input);
		}

		protected virtual Value DefaultValue()
		{
			return null;
		}

		void Parse()
		{
			var operators = new Stack<Operator>();
			var expressions = new Stack<Expression>();
			var takeop = false;
			var maxLevel = -100;
			var offset = _offset;
			var shadow = _input;

			while (shadow.Length > 0)
			{
				switch (shadow[0])
				{
					case ' ':
					case '\t':
					case '\r':
					case '\n':
						_lastSkip = shadow[0];
						offset++;
						shadow = shadow.Substring(1);
						continue;
				}

				if (takeop)
				{
					var op = FindOperator(shadow);

					if (!op.ExpectExpression)
						expressions.Push(new TreeExpression(op, expressions.Pop()));
					else
					{
						if (op.Level >= (op.IsRightToLeft ? maxLevel : maxLevel + 1))
							maxLevel = op.Level;
						else
						{
							while (true)
							{
								var right = expressions.Pop();
								var left = expressions.Pop();
								expressions.Push(new TreeExpression(operators.Pop(), left, right));

								if (operators.Count == 0 || operators.Peek().Level <= op.Level)
								{
									maxLevel = op.Level;
									break;
								}
							}
						}

						takeop = false;
						operators.Push(op);
					}

					shadow = op.Set(shadow);
					offset += op.Input.Length;
				}
				else
				{
					var exp = Tokens.Instance.FindExpression(_query, shadow);
					exp.Offset = offset;
					expressions.Push(exp);
					shadow = exp.Set(shadow);
					offset += exp.Input.Length;
					takeop = true;
				}
			}

			if(expressions.Count > 0 && operators.Count != expressions.Count - 1)
				throw new ExpressionNotFoundException(_input.Substring(_input.Length < 5 ? 0 : _input.Length - 5));

			while (operators.Count > 1)
			{
				var right = expressions.Pop();
				var left = expressions.Pop();
				expressions.Push(new TreeExpression(operators.Pop(), left, right));
			}

			if (operators.Count == 1)
			{
				Operator = operators.Pop();
				var right = expressions.Pop();
				var left = expressions.Pop();
				_expressions = new Expression[] { left, right };
			}
			else if (expressions.Count == 1)
			{
				_expressions = new Expression[] { expressions.Pop() };
			}
		}

		internal virtual Value Interpret(Dictionary<string, Value> symbols)
		{
			if (Operator != null)
				return Operator.Evaluate(Expressions, symbols);
			else if (HasContent)
				return Expressions[0].Interpret(symbols);
				
			return DefaultValue();
		}

		public override string ToString()
		{
			if (!HasContent)
				return string.Empty;

			var sb = new StringBuilder();
			sb.Append(PrintExpression(Expressions[0]));

			if (Operator != null)
			{
				sb.Append("+").AppendLine(Operator.ToString());

				if (Expressions.Length == 2)
					sb.Append(PrintExpression(Expressions[1]));
			}

			return sb.ToString();
		}

		string PrintExpression(Expression exp)
		{
			var lines = exp.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			var sb = new StringBuilder();

			foreach (var line in lines)
				sb.Append("+").AppendLine(line);

			return sb.ToString();
		}

		#endregion
	}
}
