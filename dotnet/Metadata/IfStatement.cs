using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class IfStatement : Statement
    {
        private Expression expression;
        private Statement statement;
        private Statement elseStatement;
        private TypeReference boolType;
        private bool returns = false;

        public IfStatement(ILocation location, Expression expression, Statement statement, Statement elseStatement)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (statement == null)
                throw new ArgumentNullException("statement");
            this.expression = expression;
            this.statement = statement;
            this.elseStatement = elseStatement;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Statement elseStatementInst;
            if (elseStatement == null)
                elseStatementInst = null;
            else
                elseStatementInst = elseStatement.InstantiateTemplate(parameters);
            return new IfStatement(this, expression.InstantiateTemplate(parameters),
                statement.InstantiateTemplate(parameters), elseStatementInst);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
            expression.Resolve(generator);
            statement.Resolve(generator);
            if (elseStatement != null)
                elseStatement.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            statement.Prepare(generator);
            if (elseStatement != null)
                elseStatement.Prepare(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            expression.Prepare(generator, null); // boolean
            expression.Generate(generator); // boolean
            boolType.GenerateConversion(this, generator, expression.TypeReference);
            JumpToken elseToken = generator.Assembler.CreateJumpToken();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.JumpIfFalse(elseToken);
            generator.Resolver.EnterContext();
            if (statement.IsEmptyBlock())
                throw new CompilerException(statement, string.Format(Resource.Culture, Resource.IfBranchIsEmpty));
            statement.Generate(generator, returnType);
            Resolver.Context trueContext = generator.Resolver.LeaveContextAcquire();
            if (elseStatement != null)
            {
                if (elseStatement.IsEmptyBlock())
                    throw new CompilerException(elseStatement, string.Format(Resource.Culture, Resource.IfBranchIsEmpty));
                JumpToken skipElseToken = generator.Assembler.CreateJumpToken();
                generator.Assembler.Jump(skipElseToken);
                generator.Assembler.SetDestination(elseToken);
                generator.Resolver.EnterContext();
                elseStatement.Generate(generator, returnType);
                returns = statement.Returns() && elseStatement.Returns();
                Resolver.Context falseContext = generator.Resolver.LeaveContextAcquire();
                generator.Resolver.IntersectContexts(trueContext, falseContext);
                generator.Resolver.ReleaseContext(falseContext);
                generator.Assembler.SetDestination(skipElseToken);
            }
            else
                generator.Assembler.SetDestination(elseToken);
            generator.Resolver.ReleaseContext(trueContext);
        }

        public override bool Returns()
        {
            return returns;
        }
    }
}
