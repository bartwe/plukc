using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public abstract class PostfixExpression : Expression
    {
        protected PostfixExpression(ILocation location)
            : base(location)
        {
        }

        public abstract void SetParent(Expression parent);
    }
}
