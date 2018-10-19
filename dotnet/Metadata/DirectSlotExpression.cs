using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class DirectSlotExpression : Expression, IIncompleteSlotAssignment
    {
        private int slot = int.MinValue;
        private TypeReference type;
        private bool allowIncomplete;

        public DirectSlotExpression(ILocation location, int slot, TypeReference type)
            : base(location)
        {
            this.slot = slot;
            this.type = type;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Require.NotCalled();
            return null;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return false;
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Resolver.RetrieveSlot(this, slot, allowIncomplete);
            generator.Assembler.RetrieveVariable(slot);
        }

        public override TypeReference TypeReference { get { Require.Assigned(type); return type; } }

        public override bool NeedsToBeStored()
        {
            return false;
        }

        public override bool HasSideEffects()
        {
            return false;
        }

        public void AllowRetrieval()
        {
            allowIncomplete = true;
        }

        public bool IsIncompleteSlot()
        {
            return allowIncomplete;
        }
    }
}
