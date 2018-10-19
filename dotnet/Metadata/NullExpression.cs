using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class NullExpression : Expression
    {
        TypeReference nullType;

        public NullExpression(ILocation location)
            : base(location)
        { }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new NullExpression(this);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            nullType = generator.Resolver.ResolveType(this, new TypeName(new Identifier(this, "void")));
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.Empty();
        }

        public override TypeReference TypeReference { get { Require.Assigned(nullType); return nullType; } }
    }
}
