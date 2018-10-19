using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class ReturnStatement : Statement
    {
        private Expression expression;

        public ReturnStatement(ILocation location, Expression expression)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            this.expression = expression;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new ReturnStatement(this, expression.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            if (returnType == null)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.ReturnStatementNotAllowed));
            expression.Prepare(generator, returnType);
            expression.Generate(generator);
            returnType.GenerateConversion(this, generator, expression.TypeReference);
            DoReturn(this, generator);
        }

        public static void DoReturn(ILocation location, Generator generator)
        {
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, location);
            bool tryContext;
            JumpToken gotoToken = generator.Resolver.FindGoto("@return", out tryContext);
            if (tryContext)
            {
                generator.Assembler.MarkType();
                generator.Assembler.ExceptionHandlerInvoke();
            }
            else
            {
                if (gotoToken == null)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.UnsupportedJumpOutOfTry));
                generator.Assembler.Jump(gotoToken);
            }
        }

        public override bool Returns()
        {
            return true;
        }
    }
}
