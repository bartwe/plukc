using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class RecurStatement : Statement
    {
        private List<Expression> parameters = new List<Expression>();

        public RecurStatement(ILocation location)
            : base(location)
        {
        }

        public void AddParameter(Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            parameters.Add(expression);
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            RecurStatement copy = new RecurStatement(this);
            foreach (Expression parameter in this.parameters)
                copy.AddParameter(parameter.InstantiateTemplate(parameters));
            return copy;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            foreach (Expression parameter in this.parameters)
                parameter.Resolve(generator);
        }

        private string ParameterListString(System.Collections.Generic.IEnumerable<ParameterMetadata> list)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            bool first = true;
            foreach (ParameterMetadata p in list)
            {
                TypeReference tr = p.TypeReference;
                if (first)
                    first = false;
                else
                    sb.Append(", ");
                sb.Append(tr.TypeName.Data);
            }
            sb.Append(")");
            return sb.ToString();
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            Parameters scopeParameters = generator.Resolver.CurrentContextParameters();

            Require.Assigned(scopeParameters);
            if (scopeParameters.ParameterList.Count != this.parameters.Count)
                throw new CompilerException(this, string.Format(Resource.Culture,
                    Resource.FunctionCallParameterCountMismatch, ParameterListString(scopeParameters.ParameterList), this.parameters.Count));

            int i = 0;
            foreach (Expression parameter in this.parameters)
            {
                TypeReference tr = scopeParameters.ParameterList[i].TypeReference;
                parameter.Prepare(generator, tr);
                i++;
            }
            i = 0;
            foreach (Expression parameter in this.parameters)
            {
                TypeReference tr = scopeParameters.ParameterList[i].TypeReference;
                parameter.Generate(generator);
                tr.GenerateConversion(this, generator, parameter.TypeReference);
                i++;
                generator.Assembler.PushValue();
            }
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            //store in local slots
            List<ParameterMetadata> p = new List<ParameterMetadata>(scopeParameters.ParameterList);
            p.Reverse();
            foreach (ParameterMetadata parameter in p)
            {
                generator.Assembler.PopValue();
                generator.Assembler.StoreVariable(parameter.Slot);
            }

            bool tryContext;
            JumpToken gotoToken = generator.Resolver.FindGoto("@recur", out tryContext);
            if ((gotoToken == null) || tryContext)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.UnsupportedJumpOutOfTry));
            generator.Assembler.Jump(gotoToken);
        }

        public override bool Returns()
        {
            return true;
        }
    }
}
