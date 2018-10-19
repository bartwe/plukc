using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Identifier : ILocation
    {
        private ILocation location;
        private string value;
        private string _namespace;

        public int Line { get { return location.Line; } }
        public int Column { get { return location.Column; } }
        public string Source { get { return location.Source; } }
        public string Data { get { return value; } }
        public string Namespace
        {
            get
            {
                if (_namespace == null)
                {
                    string[] parts = value.Split('.');
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < (parts.Length - 1); ++i)
                    {
                        if (i != 0)
                            sb.Append(".");
                        sb.Append(parts[i]);
                    }
                    _namespace = sb.ToString();
                }
                return _namespace;
            }
        }

        public static string ParentNamespace(string namespace_)
        {
            string[] parts = namespace_.Split('.');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < (parts.Length - 1); ++i)
            {
                if (i != 0)
                    sb.Append(".");
                sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        public Identifier(ParserToken token)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            this.location = token;
            value = token.Token;
        }

        public Identifier(ILocation location, string value)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            if (string.IsNullOrEmpty(value))
                throw new ArgumentOutOfRangeException("value");
            this.location = location;
            this.value = value;
        }

        public void PrettyPrint(StringBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            builder.Append(value);
        }

        public override string ToString()
        {
            return Data;
        }
    }
}
