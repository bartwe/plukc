using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;
using System.Runtime.Serialization;

namespace Compiler
{
    [Serializable]
    public class CompilerException : Exception
    {
        private ILocation location;
        private string message;

        public CompilerException(ILocation location, string message)
            : base(LocationToString(location) + " " + message)
        {
            this.location = location;
            this.message = message;
        }

        public CompilerException()
        {
        }

        public CompilerException(string message)
            : base(message)
        {
        }
        public CompilerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
        protected CompilerException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

	public string Error { get { return message; } }

        public string ErrorMessage
        {
            get
            {
                if (location == null)
                    return Message;
                return LocationToString(location) + " error: " + message;
            }
        }

        public string WarningMessage
        {
            get
            {
                if (location == null)
                    return Message;
                return LocationToString(location) + " warning: " + message;
            }
        }

        private static string LocationToString(ILocation location)
        {
            if (location != null)
                return location.Source + ":" + location.Line + ":" + location.Column + ":";
            else
                return "-";
        }
    }
}
