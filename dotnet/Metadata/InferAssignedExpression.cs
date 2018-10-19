using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class InferAssignedExpression : Expression
    {
        private Expression parent;
        private TypeReference type;
        private bool suppressed;

        public InferAssignedExpression(ILocation location, Expression parent)
            : base(location)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            this.parent = parent;
        }

        public override TypeReference TypeReference
        {
            get
            {
                Require.Assigned(type);
                return type;
            }
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new InferAssignedExpression(this, parent.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            if ((inferredHint != null) && !(inferredHint is NullableTypeReference))
                inferredHint = new NullableTypeReference(inferredHint);
            return parent.NeedsInference(generator, inferredHint);
        }

        public override void Prepare (Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            if (inferredType != null)
            {
                if (inferredType is NullableTypeReference)
                    suppressed = true;
                else
                    inferredType = new NullableTypeReference(inferredType);
            }
            parent.Prepare(generator, inferredType);
            type = parent.TypeReference;
            if (type is NullableTypeReference)
                type = ((NullableTypeReference)type).Parent;
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
            if (suppressed) return;
            if (parent.TypeReference is NullableTypeReference)
            {
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Assembler.CrashIfNull();
            }
        }
    }
}
