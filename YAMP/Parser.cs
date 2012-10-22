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
using System.Text;
using System.Threading;
using System.Reflection;
using System.Collections;
using System.ComponentModel;
using System.Collections.Generic;

namespace YAMP
{
	/// <summary>
	/// The YAMP interaction class.
	/// </summary>
	public class Parser
	{
		#region Members
		
		QueryContext _query;

        static ParseContext primary;
		
		#endregion

        #region static Events

        public static event Action<QueryContext, Exception> OnExecuted;

        #endregion

        #region ctor

        private Parser(ParseContext context, QueryContext query)
        {
            _query = query;
            query.Context = context;
            query.Interpreter = new ParseTree(context, query.Input);
        }

        #endregion

        #region Static constructions

        /// <summary>
		/// Creates the parse tree for the given expression within the root context.
		/// </summary>
		/// <param name="input">
		/// The expression to evaluate.
        /// </param>
        /// <returns>The parser instance.</returns>
		public static Parser Parse(string input)
		{
			return new Parser(PrimaryContext, new QueryContext(input));
		}

        /// <summary>
        /// Creates the parse tree for the given expression within a specific context.
        /// </summary>
        /// <param name="context">
        /// The context that the parser should use.
        /// </param>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        /// <returns>The parser instance.</returns>
        public static Parser Parse(ParseContext context, string input)
        {
            return new Parser(context, new QueryContext(input));
        }
		
		#endregion

        #region Async methods

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously (followed by a continuation with the OnExecuted event).
        /// </summary>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        public static void ExecuteAsync(string input)
        {
            ExecuteAsync(ParseContext.Default, input);
        }

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously (followed by a continuation with the OnExecuted event).
        /// </summary>
        /// <param name="context">
        /// The context of the evaluation.
        /// </param>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        public static void ExecuteAsync(ParseContext context, string input)
        {
            var continuation = OnExecuted;

            if (continuation == null)
                continuation = (v, e) => { };

            ExecuteAsync(context, input, null, continuation);
        }

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously.
        /// </summary>
        /// <param name="context">
        /// The context of the evaluation.
        /// </param>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        /// <param name="continuation">
        /// The continuation action to invoke after the evaluation finished.
        /// </param>
        public static void ExecuteAsync(ParseContext context, string input, Action<QueryContext, Exception> continuation)
        {
            ExecuteAsync(context, input, null, continuation);
        }

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously.
        /// </summary>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        /// <param name="continuation">
        /// The continuation action to invoke after the evaluation finished.
        /// </param>
        public static void ExecuteAsync(string input, Action<QueryContext, Exception> continuation)
        {
            ExecuteAsync(ParseContext.Default, input, null, continuation);
        }

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously.
        /// </summary>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        /// <param name="variables">
        /// The variables to consider from external.
        /// </param>
        /// <param name="continuation">
        /// The continuation action to invoke after the evaluation finished.
        /// </param>
        public static void ExecuteAsync(string input, Hashtable variables, Action<QueryContext, Exception> continuation)
        {
            ExecuteAsync(ParseContext.Default, input, variables, continuation);
        }

        /// <summary>
        /// Creates the parse tree and evaluates the expression asynchronously.
        /// </summary>
        /// <param name="context">
        /// The context of the evaluation.
        /// </param>
        /// <param name="input">
        /// The expression to evaluate.
        /// </param>
        /// <param name="variables">
        /// The variables to consider from external.
        /// </param>
        /// <param name="continuation">
        /// The continuation action to invoke after the evaluation finished.
        /// </param>
        public static void ExecuteAsync(ParseContext context, string input, Hashtable variables, Action<QueryContext, Exception> continuation)
        {
            var worker = new AsyncTask();
            worker.Continuation = continuation;
            worker.RunWorkerAsync(new object[] { context, input, variables });
            worker.DoWork += new DoWorkEventHandler(taskInitialized);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(taskCompleted);
        }

        static void taskInitialized(object sender, DoWorkEventArgs e)
        {
            var parameters = e.Argument as object[];
            var context = parameters[0] as ParseContext;
            var input = parameters[1] as string;
            var variables = parameters[2] as Hashtable;
            var parser = new Parser(context, new QueryContext(input));
            parser.Execute(variables);
            e.Result = parser.Context;
        }

        static void taskCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var worker = sender as AsyncTask;
            worker.Continuation(e.Result as QueryContext, e.Error);
        }

        #endregion

        #region Properties

        /// <summary>
		/// Gets the context of the current parser instance (expression, value, ...).
		/// </summary>
		/// <value>
		/// The current context of this parser instance.
		/// </value>
		public QueryContext Context
		{
			get { return _query; }
		}

        /// <summary>
        /// Gets the primary context of the parser (not the root context).
        /// </summary>
        /// <value>
        /// The parser's primary context.
        /// </value>
        public static ParseContext PrimaryContext
        {
            get 
            {
                if (primary == null)
                    return Load();

                return primary; 
            }
        }
		
		#endregion
		
		#region Execution

		/// <summary>
		/// Execute the evaluation of this parser instance without any external symbols.
        /// </summary>
        /// <returns>The value from the evaluation.</returns>
		public Value Execute()
		{
			return Execute(new Hashtable());
		}

		/// <summary>
		/// Execute the evaluation of this parser instance with external symbols.
		/// </summary>
		/// <param name="values">
		/// The values in an Hashtable containing string (name), Value (value) pairs.
		/// </param>
        /// <returns>The value from the evaluation.</returns>
		public Value Execute (Hashtable values)
		{
            _query.Interpret(values);			
			return _query.Output;
		}

		/// <summary>
		/// Execute the evaluation of this parser instance with external symbols.
		/// </summary>
		/// <param name="values">
		/// The values in an anonymous object - containing name - value pairs.
        /// </param>
        /// <returns>The value from the evaluation.</returns>
		public Value Execute(object values)
		{
			var symbols = new Hashtable();
			
			if(values != null)
			{
				var props = values.GetType().GetProperties();
				
				foreach(var prop in props)
					symbols.Add(prop.Name, prop.GetValue(values, null));
			}

			return Execute(symbols);
		}
		
		#endregion
		
		#region Customization

		/// <summary>
		/// Adds a custom constant to the parser (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the constant.
		/// </param>
		/// <param name="constant">
		/// The value of the constant.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext AddCustomConstant(string name, double constant)
        {
            return AddCustomConstant(PrimaryContext, name, constant);
        }

        /// <summary>
        /// Adds a custom constant to the parser using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where this constant should be made available.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the constant.
        /// </param>
        /// <param name="constant">
        /// The value of the constant.
        /// </param>
        /// <returns>The given context.</returns>
		public static ParseContext AddCustomConstant(ParseContext context, string name, double constant)
		{
			context.AddConstant(name, constant);
            return context;
		}

		/// <summary>
        /// Adds a custom constant to the parser (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the constant.
		/// </param>
		/// <param name="constant">
		/// The value of the constant.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext AddCustomConstant(string name, Value constant)
        {
            return AddCustomConstant(PrimaryContext, name, constant);
        }

        /// <summary>
        /// Adds a custom constant to the parser using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where this constant should be made available.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the constant.
        /// </param>
        /// <param name="constant">
        /// The value of the constant.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext AddCustomConstant(ParseContext context, string name, Value constant)
		{
			context.AddConstant(name, constant);
            return context;
		}

		/// <summary>
        /// Removes a custom constant (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the constant that should be removed.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext RemoveCustomConstant(string name)
        {
            return RemoveCustomConstant(PrimaryContext, name);
        }

        /// <summary>
        /// Removes a custom constant using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where this constant should be removed.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the constant that should be removed.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext RemoveCustomConstant(ParseContext context, string name)
		{
			context.RemoveConstant(name);
            return context;
		}

		/// <summary>
        /// Adds a custom function to be used by the parser (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the function that should be added.
		/// </param>
		/// <param name="f">
		/// The function that fulfills the signature Value f(Value v).
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext AddCustomFunction(string name, FunctionDelegate f)
        {
            return AddCustomFunction(PrimaryContext, name, f);
        }

        /// <summary>
        /// Adds a custom function to be used by the parser using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where this function should be made available.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the function that should be added.
        /// </param>
        /// <param name="f">
        /// The function that fulfills the signature Value f(Value v).
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext AddCustomFunction(ParseContext context, string name, FunctionDelegate f)
		{
			context.AddFunction(name, new ContainerFunction(name, f));
            return context;
		}

		/// <summary>
        /// Removes a custom function (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the function that should be removed.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext RemoveCustomFunction(string name)
        {
            return RemoveCustomFunction(PrimaryContext, name);
        }

        /// <summary>
        /// Removes a custom function using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where this function should be removed.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the function that should be removed.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext RemoveCustomFunction(ParseContext context, string name)
		{
			context.RemoveFunction(name);
            return context;
		}

		/// <summary>
        /// Adds a variable to be used by the parser (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the variable that should be added.
		/// </param>
		/// <param name="value">
		/// The value of the variable.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext AddVariable(string name, Value value)
        {
            return AddVariable(PrimaryContext, name, value);
        }

        /// <summary>
        /// Adds a variable to be used by the parser using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where the variable should be made available.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the variable that should be added.
        /// </param>
        /// <param name="value">
        /// The value of the variable.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext AddVariable(ParseContext context, string name, Value value)
		{
            context.Variables.Add(name, value);
            return context;
		}

		/// <summary>
        /// Removes a variable from the workspace (to the primary context).
		/// </summary>
		/// <param name="name">
		/// The name of the symbol corresponding to the variable that should be removed.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext RemoveVariable(string name)
        {
            return RemoveVariable(PrimaryContext, name);
        }

        /// <summary>
        /// Removes a variable from the workspace using a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where the variable should be removed from.
        /// </param>
        /// <param name="name">
        /// The name of the symbol corresponding to the variable that should be removed.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext RemoveVariable(ParseContext context, string name)
		{
            if(context.Variables.ContainsKey(name))
                context.Variables.Remove(name);

            return context;
		}

        /// <summary>
        /// Loads an external library (assembly) that uses IFunction, Operator, ..., into the primary context.
        /// </summary>
        /// <param name="assembly">
        /// The assembly to load as a plugin.
        /// </param>
        /// <returns>The default context.</returns>
        public static ParseContext LoadPlugin(Assembly assembly)
        {
            return LoadPlugin(PrimaryContext, assembly);
        }

        /// <summary>
        /// Loads an external library (assembly) that uses IFunction, Operator, ..., into a specific context.
        /// </summary>
        /// <param name="context">
        /// The context where the new functions and constants should be available.
        /// </param>
        /// <param name="assembly">
        /// The assembly to load as a plugin.
        /// </param>
        /// <returns>The given context.</returns>
        public static ParseContext LoadPlugin(ParseContext context, Assembly assembly)
        {
            Tokens.Instance.RegisterAssembly(context, assembly);
            return context;
        }

        /// <summary>
        /// Load the required functions, operators and expressions (CAN only be performed once).
        /// </summary>
        public static ParseContext Load()
        {
            Tokens.Instance.Touch();

            if(primary == null)
                primary = new ParseContext(ParseContext.Default);

            return primary;
        }
		
		#endregion
		
		#region General
		
		public override string ToString ()
		{
            var sb = new StringBuilder();
            sb.Append("YAMP == VERSION ").Append(Assembly.GetExecutingAssembly().GetName().Version).AppendLine(" ==");
            //sb.Append("YAMP [ input = ").Append(_query.Original).AppendLine(" ]");
            //sb.AppendLine("--------------");
            sb.Append(_query);
            return sb.ToString();
		}
		
		#endregion
	}
}