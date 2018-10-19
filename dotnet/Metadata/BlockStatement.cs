using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class BlockStatement : Statement
    {
        List<Statement> statements = new List<Statement>();
        ILocation closing;
        bool returns = false;

        public BlockStatement(ILocation location)
            : base(location)
        {
        }

        public void SetClosingLocation(ILocation location)
        {
            Require.Assigned(location);
            this.closing = location;
        }

        public void AddStatement(Statement statement)
        {
            if (statement == null)
                throw new ArgumentNullException("statement");
            statements.Add(statement);
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            BlockStatement block = new BlockStatement(this);
            block.SetClosingLocation(closing);
            foreach (Statement statement in statements)
                block.AddStatement(statement.InstantiateTemplate(parameters));
            return block;
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
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Resolver.EnterContext();
            
            foreach (Statement statement in statements)
            {
                if (returns)
                    if (!Program.AllowUnreadAndUnusedVariablesFieldsAndExpressions)
                        throw new CompilerException(statement, string.Format(Resource.Culture, Resource.UnreachableCode));
                    else
                        Program.Warn(new CompilerException(this, string.Format(Resource.Culture, Resource.UnreachableCode)));
                        
                statement.Generate(generator, returnType);
                returns = statement.Returns();
            }
            generator.Resolver.LeaveAndMergeContext();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, closing);
        }

        public override bool Returns()
        {
            return returns;
        }

        public override bool IsEmptyBlock()
        {
            foreach (Statement s in statements)
                if (!s.IsEmptyBlock())
                    return false;
            return true;
        }
    }
}
