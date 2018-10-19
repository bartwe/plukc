using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    interface IPossibleTypeName
    {
        bool IsPossibleTypeName();
        Identifier GetTypeIdentifier();
        void HasTypeName();
        bool UseTypeName();
    }
}
