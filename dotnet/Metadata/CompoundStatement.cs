using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class CompoundStatement : Statement
    {
        private List<Statement> statements = new List<Statement>();

        public CompoundStatement(ILocation location)
            : base(location)
        {
        }

        public void Add(Statement statement)
        {
            Require.Assigned(statement);
            statements.Add(statement);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            foreach (Statement statement in statements)
                statement.Resolve(generator);
        }

        public override void Prepare(Generator generator)
        {
            base.Prepare(generator);
            foreach (Statement statement in statements)
                statement.Prepare(generator);
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            foreach (Statement statement in statements)
                statement.Generate(generator, returnType);
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            CompoundStatement result = new CompoundStatement(this);
            foreach (Statement statement in statements)
                result.Add(statement.InstantiateTemplate(parameters));
            return result;
        }
    }
}
