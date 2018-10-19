using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public abstract class Expression : NodeBase
    {
        protected Expression(ILocation location)
            : base(location)
        {
        }

        public abstract Expression InstantiateTemplate(Dictionary<string, TypeName> parameters);

        private bool resolved;
        //private string cs;

        public bool Resolved { get { return resolved; } }

        public virtual void Resolve(Generator generator)
        {
            Require.False(resolved, "Internal error, Expression is already resolved");
            resolved = true;
        }

        bool needsInference;
        bool calledNeedsInference;

        public bool NeedsInference(Generator generator, TypeReference inferredHint)
        {
            if (calledNeedsInference)
                return needsInference;
            calledNeedsInference = true;
            needsInference = InnerNeedsInference(generator, inferredHint);
            return needsInference;
        }

        protected virtual bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return false;
        }

        private bool prepared;

        public virtual void Prepare(Generator generator, TypeReference inferredType)
        {
            Require.True(resolved, "Internal error, Expression must be resolved before being prepared.");
//            Require.False(prepared, "Internal error, Expression is already prepared.");
            prepared = true;
        }

        public abstract TypeReference TypeReference { get; }

//        private bool generated;

        public virtual void Generate(Generator generator)
        {
            //debug
//            if (!generated)
//                cs = new System.Diagnostics.StackTrace().ToString();

            Require.True(resolved, "Internal error, Expression must be resolved before being generated.");
            Require.True(prepared, "Internal error, Expression must be prepare before being generated.");
//            Require.False(generated, "Internal error, Expression is already generated. " + cs);
//            generated = true;
        }

        internal protected virtual bool SuppliesType()
        {
            return false;
        }

        internal protected virtual void ConsumeType()
        {
            Require.NotCalled();
        }

        public virtual bool NeedsToBeStored()
        {
            return true;
        }

        public virtual bool HasSideEffects()
        {
            return false;
        }
    }
}
