using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class InitializerExpression : PostfixExpression
    {
        private Expression parent;
        private List<KeyValuePair<Identifier, Expression>> fieldInitializer = new List<KeyValuePair<Identifier, Expression>>();
        private List<List<Expression>> collectionInitializer = new List<List<Expression>>();
        private List<Expression> fieldAssignment = new List<Expression>();
        private List<Expression> collectionAddition = new List<Expression>();
        private StackTopExpression stackTopExpression;

        public InitializerExpression(ILocation location)
            : base(location)
        {
            stackTopExpression = new StackTopExpression(location);
        }

        public override void SetParent(Expression parent)
        {
            Require.Unassigned(this.parent);
            Require.Assigned(parent);
            this.parent = parent;
            stackTopExpression.parent = parent;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            InitializerExpression r = new InitializerExpression(this);
            r.SetParent(parent.InstantiateTemplate(parameters));
            foreach (KeyValuePair<Identifier, Expression> f in fieldInitializer)
                r.AddFieldInitializer(f.Key, f.Value.InstantiateTemplate(parameters));
            foreach (List<Expression> f in collectionInitializer)
            {
                List<Expression> l = new List<Expression>();
                foreach (Expression e in f)
                    l.Add(e.InstantiateTemplate(parameters));
                r.AddCollectionInitializer(l);
            }
            return r;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
            stackTopExpression.Resolve(generator);
            foreach (Expression s in fieldAssignment)
                s.Resolve(generator);
            foreach (Expression s in collectionAddition)
                s.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return parent.NeedsInference(generator, inferredHint);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            //todo: let inference to the values if possible ?
            parent.Prepare(generator, inferredType);
            stackTopExpression.Prepare(generator, null);
            foreach (Expression s in fieldAssignment)
                s.Prepare(generator, null);
            foreach (Expression s in collectionAddition)
                s.Prepare(generator, null);
        }

        public override TypeReference TypeReference
        {
            get { return parent.TypeReference; }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
            generator.Assembler.PushValue();
            foreach (Expression s in fieldAssignment)
                s.Generate(generator);
            foreach (Expression s in collectionAddition)
                s.Generate(generator);
            generator.Assembler.PopValue();
        }

        public void AddFieldInitializer(Identifier field, Expression expression)
        {
            Require.False(Resolved || expression.Resolved);
            AssignmentExpression assignment = new AssignmentExpression(this, new PrepareIsolationExpression(stackTopExpression), field, expression);
            fieldInitializer.Add(new KeyValuePair<Identifier, Expression>(field, expression));
            fieldAssignment.Add(assignment);
        }

        public void AddCollectionInitializer(List<Expression> expression)
        {
            Require.False(Resolved);
            collectionInitializer.Add(expression);
            CallExpression call = new CallExpression(this, new FieldExpression(this, new PrepareIsolationExpression(stackTopExpression), new Identifier(this, "Add")));
            foreach (Expression e in expression)
                call.AddParameter(e);
            collectionAddition.Add(call);
        }

        private class StackTopExpression : Expression
        {
            public Expression parent;

            public StackTopExpression(ILocation location)
                : base(location)
            {
            }

            public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
            {
                throw new NotImplementedException();
            }

            public override TypeReference TypeReference
            {
                get { return parent.TypeReference; }
            }

            public override void Generate(Generator generator)
            {
                base.Generate(generator);
                generator.Assembler.PeekValue(0);
            }
        }

        public override bool HasSideEffects()
        {
            return true;
        }

        public override bool NeedsToBeStored()
        {
            return parent.NeedsToBeStored();
        } 
    }
}
