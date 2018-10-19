using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class IsTypeExpression : PostfixExpression
    {
        AsTypeExpression inner;
        DefinitionTypeReference boolType;

        public IsTypeExpression(ILocation location, AsTypeExpression inner)
            : base(location)
        {
            Require.Assigned(inner);
            this.inner = inner;
        }

        public IsTypeExpression(ILocation location, TypeName type)
            : base(location)
        {
            inner = new AsTypeExpression(location, type);
        }

        public override void SetParent(Expression parent)
        {
            inner.SetParent(parent);
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new IsTypeExpression(this, (AsTypeExpression)inner.InstantiateTemplate(parameters));
        }

        public override TypeReference TypeReference
        {
            get { Require.Assigned(boolType); return boolType; }
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            inner.Prepare(generator, null);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            inner.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);

            generator.Assembler.IsNotNull();
            generator.Assembler.SetTypePart(boolType.RuntimeStruct);
        }

        public override bool HasSideEffects()
        {
            return inner.HasSideEffects();
        }

        public override bool NeedsToBeStored()
        {
            return inner.NeedsToBeStored();
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
            inner.Resolve(generator);
        }
    }
}
