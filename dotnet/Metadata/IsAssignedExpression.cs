using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class IsAssignedExpression : Expression
    {
        private Expression parent;
        private DefinitionTypeReference boolType;

        public IsAssignedExpression(ILocation location, Expression parent)
            : base(location)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            this.parent = parent;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
        }

        public override TypeReference TypeReference
        {
            get
            {
                Require.Assigned(boolType); return boolType;
            }
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new IsAssignedExpression(this, parent.InstantiateTemplate(parameters));
        }

        public override void Prepare (Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            parent.Prepare(generator, null); // no clue on the inferred type
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);

            generator.Assembler.IsNotNull();
            generator.Assembler.SetTypePart(boolType.RuntimeStruct);
        }
    }
}
