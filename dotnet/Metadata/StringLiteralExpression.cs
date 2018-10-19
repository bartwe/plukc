using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class StringLiteralExpression : Expression
    {
        private String text;
        private DefinitionTypeReference literalStringType;
        private DefinitionTypeReference stringType;

        public StringLiteralExpression(ILocation location, ParserToken token)
            : base(location)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            this.text = Unescape(location, token.Token);        
        }

        public StringLiteralExpression(ILocation location, String text)
            : base(location)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            this.text = text;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new StringLiteralExpression(this, text);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            literalStringType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.StaticString")));
            stringType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.String")));
        }

        public static string Unescape(ILocation location, string text)
        {
            text = text.Remove(text.Length - 1, 1).Remove(0, 1); // trim "'s 
            StringBuilder sb = new StringBuilder();
            bool escape = false;
            foreach (char c in text)
            {
                if (escape)
                {
                    escape = false;
                    switch (c)
                    {
                        case 'n':
                            sb.Append('\n');
                            break;
                        case '"':
                            sb.Append('"');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        default:
				// todo: compiler exception and all that.
                            throw new CompilerException(location, "Unknown escape: "+c);
                    }
                }
                else
                    if (c == '\\')
                    {
                        escape = true;
                    }
                    else
                        sb.Append(c);
            }
            Require.False(escape);
            return sb.ToString();
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            Placeholder textLocation = generator.AddTextLengthPrefix(text);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.SetValue(literalStringType.RuntimeStruct, textLocation);
            stringType.GenerateConversion(this, generator, literalStringType);
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(stringType); return stringType; } }
    }
}
