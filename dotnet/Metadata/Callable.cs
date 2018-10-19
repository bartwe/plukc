using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public abstract class Callable : NodeBase
    {
        private Definition parentDefinition;

        public abstract TypeReference ReturnType { get; }
        public abstract Parameters Parameters { get; }

        public Definition ParentDefinition { get { Require.Assigned(parentDefinition); return parentDefinition; } }

        protected Callable(ILocation location)
            : base(location)
        { }

        public void SetParentDefinition(Definition parentDefinition)
        {
            if (parentDefinition == null)
                throw new ArgumentNullException("parentDefinition");
            this.parentDefinition = parentDefinition;
        }

        public abstract TypeReference AsTypeReference();
        public abstract void Resolve(Generator generator);
        public abstract void Prepare(Generator generator, Set<TypeReference> dependsUpon);
        public abstract void Generate(Generator generator);
    }
}
