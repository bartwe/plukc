using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Compiler.Metadata;
using System.Globalization;

namespace Compiler
{
    class Parser : IDisposable
    {
        private ExtendedTextReader source;
        private List<ParserToken> buffer = new List<ParserToken>();
        private bool lookaheadEnabled;
        private Stack<ParserToken> lookahead = new Stack<ParserToken>();
        private Set<string> expected = new Set<string>(StringComparer.Ordinal);
        private Set<string> keywords;
        private Set<string> multiPartNonIdentifiers;
        private int line = 1;
        private int column;
        private string sourceName;

        public Parser(string sourceName, TextReader source, Set<string> keywords, Set<string> multiPartNonIdentifiers)
        {
            this.sourceName = sourceName;
            this.source = new ExtendedTextReader(source);
            this.keywords = keywords;
            this.multiPartNonIdentifiers = multiPartNonIdentifiers;
        }

        public bool Match(string keyword)
        {
            expected.Put(keyword);
            return CurrentToken.Keyword == keyword;
        }

        public ParserToken Consume()
        {
            expected.Clear();
            ParserToken result = CurrentToken;
            buffer.RemoveAt(0);
            if (lookaheadEnabled)
                lookahead.Push(result);
            return result;
        }

        public void Fail()
        {
            Require.False(lookaheadEnabled);
            throw new CompilerException(CurrentToken, string.Format(Resource.Culture, Resource.UnexpectedToken, CurrentToken.Token, Expected(expected)));
        }

        public void BeginLookahead()
        {
            Require.False(lookaheadEnabled);
            lookaheadEnabled = true;
            lookahead.Clear();
        }

        public void RevertLookahead()
        {
            Require.True(lookaheadEnabled);
            lookaheadEnabled = false;
            while (lookahead.Count > 0)
                buffer.Insert(0, lookahead.Pop());
        }

        private ParserToken CurrentToken
        {
            get
            {
                if (buffer.Count == 0)
                    ReadToken();
                Require.True(buffer.Count > 0);
                return buffer[0];
            }
        }

        private static string Expected(Set<string> expected)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string x in expected)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                if (x.StartsWith("<", StringComparison.Ordinal) && x.EndsWith(">", StringComparison.Ordinal))
                    sb.Append(x);
                else
                    sb.Append("\'" + x + "\'");
            }
            return sb.ToString();
        }

        private void ReadToken()
        {
            char c = '.';
            while (true)
            {
                var p = source.Peek();
                if (p == -1) {
                    buffer.Add(new ParserToken(sourceName, line, column, "<EndOfStream>", ParserTokenKind.EndOfStream));
                    return;
                }
                c = Convert.ToChar(source.Peek());
                if (!(char.IsWhiteSpace(c) || char.IsControl(c) || char.IsSeparator(c)))
                    break;
                if (c == '\n') // newline
                {
                    column = 1;
                    line++;
                }
                else
                    column++;
                source.Read();
            }
            if (char.IsHighSurrogate(c) || char.IsLowSurrogate(c))
                throw new NotImplementedException("Due to lazyness only the BMP of unicode is supported, kick the developer.");

            if (c == '#')
            {
                //comment
                while (true)
                {
                    var p = source.Read();
                    if (p == -1) // eof
                        break;
                    c = Convert.ToChar(p);
                    if (c == '\n') // newline
                    {
                        column = 1;
                        line++;
                        break;
                    }
                }
                ReadToken();
                return;
            }
            if (c == '/')
            {
                if (!source.EndOfStream)
                {
                    char t = Convert.ToChar(source.Read());
                    c = Convert.ToChar(source.Peek());
                    if (c == '/')
                    {
                        // comment
                        while (true)
                        {
                            var p = source.Read();
                            if (p == -1) // eof
                                break;
                            c = Convert.ToChar(p);
                            if (c == '\n') // newline
                            {
                                column = 1;
                                line++;
                                break;
                            }
                        }
                        ReadToken();
                        return;
                    }
                    source.Push(t);
                    c = t;
                }
            }

            StringBuilder sb = new StringBuilder();
            int initialColumn = column;

            if (char.IsDigit(c))
            {
                bool binary = false;
                sb.Append(Convert.ToChar(source.Read()));
                column++;
                if ((c == '0') && (!source.EndOfStream) && (source.Peek() == 'x'))
                {
                    binary = true;
                    source.Read();
                    column++;
                    while (true)
                    {
                        var p = source.Peek();
                        if (p == -1) //eof
                            break;
                        c = Convert.ToChar(p);
                        if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || (c == '_')))
                            break;
                        source.Read();
                        if (c != '_')
                            sb.Append(c);
                        column++;
                    }
                    long v = long.Parse(sb.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    sb.Length = 0;
                    sb.Append(v.ToString(CultureInfo.InvariantCulture));
                }
                else if ((c == '0') && (!source.EndOfStream) && (source.Peek() == 'y'))
                {
                    binary = true;
                    source.Read();
                    column++;
                    long value = 0;
                    while (true) {
                        var p = source.Peek();
                        if (p == -1) //eof
                            break;
                        c = Convert.ToChar(p);
                        if (!((c >= '0' && c <= '1') || (c == '_')))
                            break;
                        source.Read();
                        if (c != '_')
                        {
                            value <<= 1;
                            if (c == '1')
                                value += 1;
                        }
                        column++;
                    }
                    sb.Length = 0;
                    sb.Append(value.ToString(CultureInfo.InvariantCulture));
                }
                else if ((c == '0') && (!source.EndOfStream) && (source.Peek() == 'c'))
                {
                    binary = true;
                    source.Read();
                    column++;
                    long value = 0;
                    while (true) {
                        var p = source.Peek();
                        if (p == -1) //eof
                            break;
                        c = Convert.ToChar(p);
                        if (!((c >= '0' && c <= '7') || (c == '_')))
                            break;
                        source.Read();
                        if (c != '_')
                        {
                            value <<= 3;
                            int cc = int.Parse(c.ToString(), CultureInfo.InvariantCulture);
                            value += cc;
                        }
                        column++;
                    }
                    sb.Length = 0;
                    sb.Append(value.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    while (true) {
                        var p = source.Peek();
                        if (p == -1) //eof
                            break;
                        c = Convert.ToChar(p);
                        if (!(char.IsDigit(c) || (c == '_')))
                            break;
                        source.Read();
                        if (c != '_')
                            sb.Append(c);
                        column++;
                    }
                    if (c == '.')
                    {
                        source.Read();
                        column++;
                        bool lookahead = true;
                        while (!source.EndOfStream)
                        {
                            c = Convert.ToChar(source.Peek());
                            if (!(char.IsDigit(c) || (c == '_')))
                                break;
                            if (lookahead)
                            {
                                sb.Append('.');
                                column++;
                                lookahead = false;
                            }
                            source.Read();
                            if (c != '_')
                                sb.Append(c);
                            column++;
                        }
                        if (lookahead)
                        {
                            c = '.';
                            column--;
                            source.Push('.');
                        }
                    }
                    if ((c == 'e') || (c == 'E'))
                    {
                        sb.Append(Convert.ToChar(source.Read()));
                        column++;
                        if (!source.EndOfStream)
                        {
                            c = Convert.ToChar(source.Peek());
                            if ((c == '+') || (c == '-'))
                            {
                                sb.Append(Convert.ToChar(source.Read()));
                                column++;
                            }
                        }
                        while (true) {
                            var p = source.Peek();
                            if (p == -1) //eof
                                break;
                            c = Convert.ToChar(p);
                            if (!(char.IsDigit(c) || (c == '_')))
                                break;
                            source.Read();
                            if (c != '_')
                                sb.Append(c);
                            column++;
                        }
                    }
                }
                if (binary)
                    sb.Append("#");
                buffer.Add(new ParserToken(sourceName, line, initialColumn, sb.ToString(), ParserTokenKind.Number));
            }
            else if (char.IsLetter(c) || (c == '_'))
            {
                sb.Append(Convert.ToChar(source.Read()));
                column++;
                while (true) {
                    var p = source.Peek();
                    if (p == -1) //eof
                        break;
                    c = Convert.ToChar(p);
                    if (!(char.IsLetterOrDigit(c) || (c == '_')))
                        break;
                    sb.Append(Convert.ToChar(source.Read()));
                    column++;
                }
                string token = sb.ToString();
                if (keywords.Contains(token))
                    buffer.Add(new ParserToken(sourceName, line, initialColumn, token, ParserTokenKind.Keyword));
                else
                    buffer.Add(new ParserToken(sourceName, line, initialColumn, token, ParserTokenKind.Identifier));
            }
            else if (c == '"')
            {
                sb.Append(Convert.ToChar(source.Read()));
                column++;
                bool escaped = false;
                while (true) {
                    var p = source.Read();
                    if (p == -1) //eof
                        break;
                    c = Convert.ToChar(p);
                    column++;
                    sb.Append(c);
                    if (c == '\n') // newline
                    {
                        column = 0;
                        line++;
                    }
                    if ((c == '"') && !escaped)
                    {
                        break;
                    }
                    if (c == '\\')
                        escaped = escaped ^ true;
                    else
                        escaped = false;
                }
                buffer.Add(new ParserToken(sourceName, line, initialColumn, sb.ToString(), ParserTokenKind.String));
                return;
            }
            else
            {
                // operator (i guess)
                sb.Append(Convert.ToChar(source.Read()));
                column++;
                while (true) {
                    var p = source.Peek();
                    if (p == -1) //eof
                        break;
                    c = Convert.ToChar(p);
                    if ((!(char.IsLetterOrDigit(c) ||
                            (c == '_') ||
                            char.IsWhiteSpace(c) ||
                            char.IsSeparator(c) ||
                            char.IsControl(c) ||
                            char.IsHighSurrogate(c) ||
                            char.IsLowSurrogate(c))
                        ) &&
                        multiPartNonIdentifiers.Contains(sb.ToString() + c.ToString()))
                    {
                        sb.Append(Convert.ToChar(source.Read()));
                        column++;
                    }
                    else
                        break;
                }
                buffer.Add(new ParserToken(sourceName, line, initialColumn, sb.ToString(), ParserTokenKind.Symbol));
            }
        }

        private sealed class ExtendedTextReader : IDisposable
        {
            private char[] rbuffer;
            private int length;
            private int offset;
            private int? peeked;
            private TextReader reader;
            private Stack<char> buffer = new Stack<char>();

            public ExtendedTextReader(TextReader reader)
            {
                if (reader == null)
                    throw new ArgumentNullException("reader");
                this.reader = reader;
                rbuffer = new char[4096];
                length = 0;
                offset = 0;
            }

            public void Dispose()
            {
                reader.Dispose();
            }

            private void FillBuf()
            {
                if (length == -1) return; // eof
                Require.True(length - offset == 0);
                length = reader.Read(rbuffer, 0, rbuffer.Length);
                if (length == 0)
                    length = -1;
                offset = 0;
            }

            public int Peek()
            {
                if (peeked.HasValue)
                    return peeked.Value;
                if (buffer.Count > 0)
                    peeked = buffer.Peek();
                else
                {
                    if (length == offset)
                        FillBuf();
                    if (length == -1)
                        peeked = -1;
                    else
                        peeked = rbuffer[offset];
                }
                return peeked.Value;
            }

            public int Read()
            {
                peeked = null;
                if (buffer.Count > 0)
                    return buffer.Pop();

                if (length == offset)
                    FillBuf();
                if (length == -1)
                    return -1;
                else
                {
                    return rbuffer[offset++];
                }
            }

            public void Push(char c)
            {
                buffer.Push(c);
                peeked = c;
            }

            public bool EndOfStream
            { get { return (Peek() == -1); } }
        }

        #region IDisposable Members

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                if (source != null)
                {
                    source.Dispose();
                    source = null;
                }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
