using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class WithStatement : Statement
    {
        Expression expression;
        Statement statement;
        int slot = int.MinValue;
        bool returns;

        public WithStatement(ILocation location, Expression expression, Statement statement)
            : base(location)
        {
            Require.Assigned(expression);
            Require.Assigned(statement);
            this.expression = expression;
            this.statement = statement;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new WithStatement(this, expression.InstantiateTemplate(parameters), statement.InstantiateTemplate(parameters));
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            slot = generator.Assembler.AddVariable();
            statement.Prepare(generator);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
            statement.Resolve(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            expression.Prepare(generator, null);
            expression.Generate(generator);
            generator.Assembler.StoreVariable(slot);
            generator.Resolver.EnterContext();
            generator.Resolver.AddVariable(new Identifier(this, Guid.NewGuid().ToString("B")), expression.TypeReference, slot, true);
            if (!expression.TypeReference.IsDefinition && !expression.TypeReference.IsStatic)
                throw new CompilerException(this, Resource.CanOnlyUseWithOnNotNullClassInstances);
            generator.Resolver.SetImplicitFields(slot, expression.TypeReference);
            statement.Generate(generator, returnType);
            returns = statement.Returns();
            generator.Resolver.LeaveAndMergeContext();
        }

        public override bool Returns()
        {
            return returns;
        }
    }
}
