using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class ScopeStatement : Statement
    {
        private TypeName typeName;
        private TypeReference typeRef;
        private TypeReference disposable;
        private Identifier name;
        private Expression expression;
        private TryStatement statement;
        private Statement cleanup;
        private int slot = int.MinValue;
        private bool returns;

        public ScopeStatement(ILocation location, TypeName type, Identifier name, Expression expression, Statement statement)
            : base(location)
        {
            if (name == null)
                throw new ArgumentException("name");
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (statement == null)
                throw new ArgumentNullException("statement");
            this.typeName = type;
            this.name = name;
            this.expression = expression;
            this.statement = new TryStatement(this, statement);
            this.cleanup = new ExpressionStatement(this,
                new CallExpression(this,
                    new FieldExpression(this,
                        new SlotExpression(this, name, false), new Identifier(this, "Dispose"))));
            this.statement.SetFinallyStatement(this, cleanup);
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new ScopeStatement(this, typeName.InstantiateTemplate(parameters), name, expression.InstantiateTemplate(parameters), statement.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (typeName != null)
                typeRef = generator.Resolver.ResolveType(typeName, typeName);
            disposable = generator.Resolver.ResolveType(this, new TypeName(new Identifier(this, "pluk.base.Disposable")));
            expression.Resolve(generator);
            statement.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            slot = generator.Assembler.AddVariable();
            statement.Prepare(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            generator.Resolver.EnterContext();
            expression.Prepare(generator, typeRef);
            if (typeName == null)
                typeRef = expression.TypeReference;
            if (!disposable.Supports(typeRef))
                throw new CompilerException(this, Resource.CanOnlyUseScopeOnNotNullDisposable);
            expression.Generate(generator);
            typeRef.GenerateConversion(this, generator, expression.TypeReference);
            generator.Resolver.AddVariable(name, typeRef, slot, true);
            generator.Resolver.AssignSlot(slot);
            generator.Assembler.StoreVariable(slot);
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
