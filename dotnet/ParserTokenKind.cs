using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public enum ParserTokenKind
    {
        EndOfStream,
        Number,
        Identifier,
        Keyword,
        String,
        Symbol
    }
}
