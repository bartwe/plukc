using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class BooleanLiteralExpression : Expression
    {
        private bool value;
        private DefinitionTypeReference boolType;

        public bool IsTrue { get { return value; } }

        private BooleanLiteralExpression(BooleanLiteralExpression self)
            : base(self)
        {
            value = self.value;
        }

        public BooleanLiteralExpression(ILocation location, bool value)
            : base(location)
        {
            this.value = value;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new BooleanLiteralExpression(this);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.SetImmediateValue(boolType.RuntimeStruct, BoolToInt(value));
        }

        private static int BoolToInt(bool value)
        {
            if (value)
                return 1;
            else
                return 0;
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(boolType); return boolType; } }
    }
}
