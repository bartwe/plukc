using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public class ParserToken : ILocation
    {
        private int line;
        private int column;
        private string source;
        private string token;
        private string keyword;
        private ParserTokenKind kind;

        public int Line { get { return line; } }
        public int Column { get { return column; } }
        public string Source { get { return source; } }
        public string Token { get { return token; } }
        public string Keyword { get { return keyword; } }
        public ParserTokenKind Kind { get { return kind; } }

        public ParserToken(string source, int line, int column, string token, ParserTokenKind kind)
        {
            this.source = source;
            this.line = line;
            this.column = column;
            this.token = token;
            this.kind = kind;
            switch (kind)
            {
                case ParserTokenKind.String:
                    {
                        this.keyword = "<String>";
                        break;
                    }
                case ParserTokenKind.Number:
                    {
                        this.keyword = "<Number>";
                        break;
                    }
                case ParserTokenKind.EndOfStream:
                    {
                        this.keyword = "<EndOfStream>";
                        break;
                    }
                case ParserTokenKind.Identifier:
                    {
                        this.keyword = "<Identifier>";
                        break;
                    }
                case ParserTokenKind.Keyword:
                    {
                        this.keyword = token;
                        break;
                    }
                case ParserTokenKind.Symbol:
                    {
                        this.keyword = token;
                        break;
                    }
                default:
                    {
                        Require.NotCalled();
                        break;
                    }
             }
        }
    }
}
