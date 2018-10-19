using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Compiler.Metadata;
namespace Compiler.Metadata
{
    public class PrepareIsolationExpression : Expression
    {
        private Expression parent;

        public PrepareIsolationExpression(Expression parent)
            : base(parent)
        {
            Require.Assigned(parent);
            this.parent = parent;
        }

        public override TypeReference TypeReference { get { return parent.TypeReference; } }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new PrepareIsolationExpression(parent.InstantiateTemplate(parameters));
        }
    }
}
