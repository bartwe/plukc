namespace Compiler {
    public class ParserToken : ILocation {
        readonly int line;
        readonly int column;
        readonly string source;
        readonly string token;
        readonly ParserTokenKind kind;

        public int Line { get { return line; } }
        public int Column { get { return column; } }
        public string Source { get { return source; } }
        public string Token { get { return token; } }

        public string Keyword {
            get {
                switch (kind) {
                    case ParserTokenKind.String:
                        return "<String>";
                    case ParserTokenKind.Number:
                        return "<Number>";
                    case ParserTokenKind.EndOfStream:
                        return "<EndOfStream>";
                    case ParserTokenKind.Identifier:
                        return "<Identifier>";
                    case ParserTokenKind.Keyword:
                        return token;
                    case ParserTokenKind.Symbol:
                        return token;
                    default: {
                        Require.NotCalled();
                        return "";
                    }
                }
            }
        }

        public ParserTokenKind Kind { get { return kind; } }

        public ParserToken(string source, int line, int column, string token, ParserTokenKind kind) {
            this.source = source;
            this.line = line;
            this.column = column;
            this.token = token;
            this.kind = kind;
        }
    }
}
