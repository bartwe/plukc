using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class AsmExpression : Expression
    {
        ParserToken token;
        TypeReference voidType;

        public AsmExpression(ILocation location, ParserToken token)
            : base(location)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            this.token = token;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            voidType = generator.Resolver.ResolveType(this, new TypeName(new Identifier(this, "void")));
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.Raw(HexUtility.GetBytes(StringLiteralExpression.Unescape(this, token.Token)));
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new AsmExpression(this, token);
        }

        public override TypeReference TypeReference { get { Require.Assigned(voidType); return voidType; } }
    }
}
