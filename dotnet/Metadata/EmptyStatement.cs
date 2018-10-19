using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class EmptyStatement : Statement
    {
        public EmptyStatement(ILocation location)
            : base(location)
        {
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new EmptyStatement(this);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
        }

        public override bool IsEmptyStatement()
        {
            return true;
        }

        public override bool IsEmptyBlock()
        {
            return true;
        }

    }
}
