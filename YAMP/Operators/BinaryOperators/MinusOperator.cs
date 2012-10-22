using System;

namespace YAMP
{
	class MinusOperator : BinaryOperator
	{
		public MinusOperator () : base("-", 5)
		{
		}
		
		public override Value Perform (Value left, Value right)
		{
			return left.Subtract(right);
		}
	}
}

