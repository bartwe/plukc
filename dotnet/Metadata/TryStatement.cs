using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class TryStatement : Statement
    {
        private class Catch : NodeBase
        {
            public TypeName typeName;
            public TypeReference type;
            public Identifier identifier;
            public Statement statement;

            public Catch(ILocation location, TypeName typeName, Identifier identifier, Statement statement)
                : base(location)
            {
                this.typeName = typeName;
                this.identifier = identifier;
                this.statement = statement;
            }
        }

        private Statement statement;
        private List<Catch> catches = new List<Catch>();
        private ILocation finallyLocation;
        private Statement finallyStatement;
        private int valueSlot = int.MinValue;
        private int returnSlot = int.MinValue;
        private bool returns = false;

        public TryStatement(ILocation location, Statement statement)
            : base(location)
        {
            this.statement = statement;
        }

        public void SetFinallyStatement(ILocation finallyLocation, Statement finallyStatement)
        {
            Require.Unassigned(this.finallyStatement);
            Require.Assigned(finallyLocation);
            Require.Assigned(finallyStatement);
            this.finallyLocation = finallyLocation;
            this.finallyStatement = finallyStatement;
        }

        public void AddCatchStatement(ILocation location, TypeName catchTypeName, Identifier catchIdentifier, Statement catchStatement)
        {
            catches.Add(new Catch(location, catchTypeName, catchIdentifier, catchStatement));
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            TryStatement result = new TryStatement(this, statement.InstantiateTemplate(parameters));
            if (finallyStatement != null)
                result.SetFinallyStatement(finallyLocation, finallyStatement.InstantiateTemplate(parameters));
            foreach (Catch c in catches)
                result.AddCatchStatement(c, c.typeName.InstantiateTemplate(parameters), c.identifier, c.statement.InstantiateTemplate(parameters));
            return result;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            statement.Resolve(generator);
            foreach (Catch c in catches)
            {
                c.type = generator.Resolver.ResolveType(c.typeName, c.typeName);
                if (c.type.Id < 0)
                    throw new Exception(c.type.TypeName.Data + c.type.GetType());
                c.statement.Resolve(generator);
            }
            if (finallyStatement != null)
                finallyStatement.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            statement.Prepare(generator);
            valueSlot = generator.Assembler.AddVariable();
            foreach (Catch c in catches)
                c.statement.Prepare(generator);
            if (finallyStatement != null)
            {
                finallyStatement.Prepare(generator);
                returnSlot = generator.Assembler.AddVariable();
            }
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            // 
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            PlaceholderRef fonCatchJump = null;
            if (finallyStatement != null)
            {
                fonCatchJump = new PlaceholderRef();
                generator.Assembler.ExceptionHandlerSetup(fonCatchJump);
                generator.Resolver.EnterContext();
                generator.Resolver.RegisterTryContext();
            }
            PlaceholderRef onCatchJump = null;
            if (catches.Count > 0)
            {
                onCatchJump = new PlaceholderRef();
                generator.Assembler.ExceptionHandlerSetup(onCatchJump);
            }
            generator.Resolver.EnterContext();
            generator.Resolver.RegisterTryContext();
            statement.Generate(generator, returnType);
            returns = statement.Returns();
            Resolver.Context statementContext = generator.Resolver.LeaveContextAcquire();
            if (catches.Count > 0)
            {
                generator.Assembler.ExceptionHandlerRemove();
                JumpToken noExceptionJump = generator.Assembler.CreateJumpToken();
                generator.Assembler.Jump(noExceptionJump);
                generator.Assembler.SetDestination(onCatchJump);

                JumpToken noReturnJump = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfNotMarked(noReturnJump);
                generator.Assembler.UnmarkType();
                ReturnStatement.DoReturn(this, generator);
                generator.Assembler.SetDestination(noReturnJump);

                generator.Assembler.StoreVariable(valueSlot);

                List<JumpToken> escape = new List<JumpToken>();

                foreach (Catch c in catches)
                {
                    generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, c);
                    generator.Assembler.RetrieveVariable(valueSlot);
                    JumpToken noMatch = generator.Assembler.CreateJumpToken();
                    generator.Assembler.TypeConversionDynamicNotNull(c.type.Id);
                    generator.Assembler.JumpIfUnassigned(noMatch);
                    generator.Assembler.StoreVariable(valueSlot);
                    generator.Resolver.EnterContext();
                    generator.Resolver.AddVariable(c.identifier, c.type, valueSlot, true);
                    generator.Resolver.AssignSlot(valueSlot);
                    c.statement.Generate(generator, returnType);
                    returns = returns && c.statement.Returns();
                    generator.Resolver.LeaveContext();
                    JumpToken jt = generator.Assembler.CreateJumpToken();
                    escape.Add(jt);
                    generator.Assembler.Jump(jt);
                    generator.Assembler.SetDestination(noMatch);
                }
                //unhandled, rethrow
                generator.Assembler.RetrieveVariable(valueSlot);
                generator.Assembler.ExceptionHandlerInvoke();
                foreach (JumpToken jt in escape)
                    generator.Assembler.SetDestination(jt);
                generator.Assembler.SetDestination(noExceptionJump);
            }
            if (finallyStatement != null)
            {
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, finallyLocation);
                generator.Resolver.LeaveContext();
                generator.Assembler.ExceptionHandlerRemove();
                generator.Assembler.Empty();
                generator.Assembler.SetDestination(fonCatchJump);
                JumpToken fnoReturnJump = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfNotMarked(fnoReturnJump);
                generator.Assembler.UnmarkType();
                generator.Assembler.StoreVariable(returnSlot);
                generator.Assembler.SetDestination(fnoReturnJump);
                generator.Assembler.StoreVariable(valueSlot);
                generator.Resolver.EnterContext();
                finallyStatement.Generate(generator, returnType);
                generator.Resolver.LeaveAndMergeContext();
                if (catches.Count == 0) // if there are no catches we can be sure the whole try part completed and so we can use the variables initialized by it.
                    generator.Resolver.MergeContext(statementContext);

                generator.Assembler.RetrieveVariable(returnSlot);
                JumpToken fonExceptionFinallyJump = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfUnassigned(fonExceptionFinallyJump);
                ReturnStatement.DoReturn(this, generator);
                generator.Assembler.SetDestination(fonExceptionFinallyJump);

                generator.Assembler.RetrieveVariable(valueSlot);
                JumpToken fnoException = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfUnassigned(fnoException);
                generator.Assembler.ExceptionHandlerInvoke();
                generator.Assembler.SetDestination(fnoException);
            }
            generator.Resolver.ReleaseContext(statementContext);
        }

        public override bool Returns()
        {
            return returns;
        }
    }
}
