using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class ForStatement : Statement
    {
        private Expression originalExpression;
        private Expression expression;
        private Statement statement;
        private TypeName typeName;
        private TypeReference type;
        private Identifier name;
        private int slot = int.MinValue;
        private int enumeratorSlot = int.MinValue;
        private TypeReference enumeratorType;
        private Expression move;
        private Expression current;
        private Identifier enumeratorName;
        private TypeReference boolType;

        public ForStatement(ILocation location, TypeName typeName, Identifier name, Expression expression, Statement statement)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (statement == null)
                throw new ArgumentNullException("statement");
            originalExpression = expression;
            this.expression = new CallExpression(this, new FieldExpression(this, expression, new Identifier(this, "OperatorIterate")));
            this.statement = statement;
            this.typeName = typeName;
            this.name = name;

            enumeratorName = new Identifier(this, Guid.NewGuid().ToString("B"));
            move = new CallExpression(this, new FieldExpression(this, new SlotExpression(this, enumeratorName, false), new Identifier(this, "Move")));
            current = new CallExpression(this, new FieldExpression(this, new SlotExpression(this, enumeratorName, false), new Identifier(this, "Value")));
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new ForStatement(this, (typeName == null) ? null : typeName.InstantiateTemplate(parameters), name,
                originalExpression.InstantiateTemplate(parameters),
                statement.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
            if (typeName != null)
            {
                type = generator.Resolver.ResolveType(typeName, typeName);
                TypeName enumeratorTypeName = new TypeName(new Identifier(this, "Iterator"));
                enumeratorTypeName.AddTemplateParameter(typeName);
                enumeratorType = generator.Resolver.ResolveType(enumeratorTypeName, enumeratorTypeName);
            }
            expression.Resolve(generator);
            statement.Resolve(generator);
            move.Resolve(generator);
            current.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            enumeratorSlot = generator.Assembler.AddVariable();
            if (name != null)
                slot = generator.Assembler.AddVariable();
            statement.Prepare(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            if (statement.IsEmptyStatement())
                throw new CompilerException(statement, string.Format(Resource.Culture, Resource.LoopStatementHasNoBody));

            generator.Resolver.EnterContext();

            expression.Prepare(generator, null);
            expression.Generate(generator); // Iterator<slot> ?
            // create the field, and the hidden enumerator
            if (enumeratorType == null)
                enumeratorType = expression.TypeReference;
            else
                enumeratorType.GenerateConversion(expression, generator, expression.TypeReference);
            generator.Resolver.AddVariable(enumeratorName, enumeratorType, enumeratorSlot, true);
            generator.Resolver.AssignSlot(enumeratorSlot);
            // get the enumerator from the expression
            generator.Assembler.StoreVariable(enumeratorSlot);
            // start of the loop
            JumpToken loopToken = generator.Assembler.CreateJumpToken();
            generator.Assembler.SetDestination(loopToken);
            // move the enumerator and check if something is available
            move.Prepare(generator, null);
            move.Generate(generator);
            boolType.GenerateConversion(this, generator, move.TypeReference);
            JumpToken skipToken = generator.Assembler.CreateJumpToken();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.JumpIfFalse(skipToken);
            // something is available so put it in the field
            current.Prepare(generator, null);
            current.Generate(generator);
            if (type == null)
                type = current.TypeReference;
            if (name != null)
            {
                generator.Resolver.AddVariable(name, type, slot, true);
                type.GenerateConversion(this, generator, current.TypeReference);
                generator.Resolver.AssignSlot(slot);
                generator.Assembler.StoreVariable(slot);
            }
            // for body
            generator.Resolver.EnterContext();
            generator.Resolver.RegisterGoto("@continue", loopToken);
            generator.Resolver.RegisterGoto("@break", skipToken);
            statement.Generate(generator, returnType);
            generator.Resolver.LeaveContext();
            generator.Assembler.Jump(loopToken);
            generator.Assembler.SetDestination(skipToken);
            generator.Resolver.LeaveContext();
        }
    }
}
