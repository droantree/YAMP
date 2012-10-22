using System;

namespace YAMP
{
    class DotPowerOperator : DotOperator
    {
        public DotPowerOperator() : base(new PowerOperator())
        {
        }

        public override ScalarValue Operation(ScalarValue left, ScalarValue right)
        {
            return left.Power(right) as ScalarValue;
        }
    }
}

