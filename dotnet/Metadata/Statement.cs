using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public abstract class Statement : NodeBase
    {
        protected Statement(ILocation location)
            : base(location)
        {
        }

        private bool resolved;

        public virtual void Resolve(Generator generator)
        {
            Require.False(resolved, "Internal error, Statement is already resolved.");
            resolved = true;
        }

        private bool prepared;
//        string cs;

        public virtual void Prepare(Generator generator)
        {
//            if (!prepared)
//                cs = new System.Diagnostics.StackTrace().ToString();
//            Require.False(prepared, "Internal error, Statement is already prepared.: "+cs);
            Require.True(resolved);
            prepared = true;
        }

//        private bool generated;

        public virtual void Generate(Generator generator, TypeReference returnType)
        {
//            Require.False(generated, "Internal error, Statement is already generated.");
            Require.True(prepared);
//            generated = true;
        }

        public abstract Statement InstantiateTemplate(Dictionary<string, TypeName> parameters);

        public virtual bool IsEmptyStatement()
        {
            return false;
        }

        public virtual bool Returns()
        {
            return false;
        }

        public virtual bool IsEmptyBlock()
        {
            return false;
        }
    }
}
