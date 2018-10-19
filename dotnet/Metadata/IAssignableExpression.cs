using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    interface IAssignableExpression
    {
        Expression ConvertToAssignment(ILocation location, Expression value);
    }
}
