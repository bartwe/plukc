using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class WhileStatement : Statement
    {
        private Expression expression;
        private Statement statement;
        private bool breaks = false;

        public WhileStatement(ILocation location, Expression expression, Statement statement)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (statement == null)
                throw new ArgumentNullException("statement");
            this.expression = expression;
            this.statement = statement;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new WhileStatement(this, expression.InstantiateTemplate(parameters),
                statement.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
            statement.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            statement.Prepare(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            if (statement.IsEmptyStatement())
                throw new CompilerException(statement, string.Format(Resource.Culture, Resource.LoopStatementHasNoBody));
            generator.Resolver.EnterContext();
            JumpToken loopToken = generator.Assembler.CreateJumpToken();
            generator.Assembler.SetDestination(loopToken);
            expression.Prepare(generator, null); // boolean
            expression.Generate(generator);
            generator.Resolver.EnterContext();
            JumpToken skipToken = generator.Assembler.CreateJumpToken();
            generator.Resolver.RegisterGoto("@continue", loopToken);
            generator.Resolver.RegisterGoto("@break", skipToken);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.JumpIfFalse(skipToken);
            statement.Generate(generator, returnType);
            generator.Assembler.Jump(loopToken);
            generator.Assembler.SetDestination(skipToken);
            generator.Resolver.LeaveContext();
            generator.Resolver.LeaveContext();
            breaks = skipToken.JumpCount > 1;
        }

        public override bool Returns()
        {
            return (expression is BooleanLiteralExpression) && ((BooleanLiteralExpression)expression).IsTrue && !breaks;
        }
    }
}
