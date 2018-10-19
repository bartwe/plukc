using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class VarAssignmentStatement : Statement
    {
        private Identifier name;
        private int slot = int.MinValue;
        private Expression expression;

        public VarAssignmentStatement(ILocation location, Identifier name, Expression expression)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            if (name == null)
                throw new ArgumentNullException("name");
            this.expression = expression;
            this.name = name;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new VarAssignmentStatement(this, name, expression.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            slot = generator.Assembler.AddVariable();
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            expression.Prepare(generator, null); // type flows from expression to var, not the otherway for var statements
            expression.Generate(generator);
            TypeReference type = expression.TypeReference;
            generator.Resolver.AddVariable(name, type, slot, false);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Resolver.AssignSlot(slot);
            generator.Assembler.StoreVariable(slot);
        }
    }
}