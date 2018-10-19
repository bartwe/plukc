using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class GotoStatement : Statement
    {
        string token;

        public GotoStatement(ILocation location, string token)
            : base(location)
        {
            this.token = token;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new GotoStatement(this, token);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            bool tryContext;
            JumpToken gotoToken = generator.Resolver.FindGoto(token, out tryContext);
            if (gotoToken == null)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.NoEnclosingLoop));
            if (tryContext)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.UnsupportedJumpOutOfTry));
            generator.Assembler.Jump(gotoToken);
        }
    }
}
