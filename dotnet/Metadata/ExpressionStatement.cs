using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class ExpressionStatement : Statement
    {
        private Expression expression;

        public ExpressionStatement(ILocation location, Expression expression)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            this.expression = expression;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new ExpressionStatement(this, expression.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            expression.Prepare(generator, null);
            if (!expression.TypeReference.TypeName.IsVoid && (!expression.HasSideEffects() || expression.NeedsToBeStored()))
            {
                if (!Program.AllowUnreadAndUnusedVariablesFieldsAndExpressions)
                    throw new CompilerException(this, Resource.ExpressionHasNoEffect);
                else
                    Program.Warn(new CompilerException(this, Resource.ExpressionHasNoEffect));
            }
            expression.Generate(generator);
            generator.Assembler.Empty();
        }
    }
}
