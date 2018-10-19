using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;
using System.Runtime.Serialization;

namespace Compiler
{
    public class ExternException: CompilerException
    {
        public ExternException(ILocation location, string message)
            : base(location, message)
        {
        }

        public ExternException()
        {
        }

        public ExternException(string message)
            : base(message)
        {
        }
        public ExternException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected ExternException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
