using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class SlotStatement : Statement
    {
        private Identifier name;
        private TypeName typeName;
        private TypeReference type;
        private int slot = int.MinValue;
        private Statement assignment;

        public SlotStatement(ILocation location, TypeName typeName, Identifier name)
            : base(location)
        {
            if (typeName == null)
                throw new ArgumentNullException("typeName");
            if (name == null)
                throw new ArgumentNullException("name");
            this.typeName = typeName;
            this.name = name;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            SlotStatement result = new SlotStatement(this, typeName.InstantiateTemplate(parameters), name);
            if (assignment != null)
                result.AddStatement(assignment.InstantiateTemplate(parameters));
            return result;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            type = generator.Resolver.ResolveType(this, typeName);
            if (assignment != null)
                assignment.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            if (assignment != null)
                assignment.Prepare(generator);
            slot = generator.Assembler.AddVariable();
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            generator.Resolver.AddVariable(name, type, slot, false);
            if (assignment != null)
            {
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                assignment.Generate(generator, returnType);
            }
        }

        public void AddStatement(Statement statement)
        {
            Require.Unassigned(assignment);
            Require.Assigned(statement);
            assignment = statement;
        }
    }
}