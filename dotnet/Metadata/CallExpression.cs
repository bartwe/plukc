using System;
using System.Collections.Generic;
using System.Text;


namespace Compiler.Metadata
{
    public class CallExpression : PostfixExpression
    {
        Expression parent;
        List<Expression> parameters = new List<Expression>();
        TypeReference type;
        private bool parametersPreResolved;

        public CallExpression(ILocation location)
            : base(location)
        {
        }

        public CallExpression(ILocation location, Expression parent)
            : this(location)
        {
            if (parent == null)
                throw new ArgumentNullException("parent");
            this.parent = parent;
        }

        public override void SetParent(Expression parent)
        {
            Require.Unassigned(this.parent);
            this.parent = parent;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            CallExpression result = new CallExpression(this, parent.InstantiateTemplate(parameters));
            foreach (Expression param in this.parameters)
                result.AddParameter(param.InstantiateTemplate(parameters));
            return result;
        }

        public void AddParameter(Expression expression)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            parameters.Add(expression);
        }

        public void ParametersPreResolved()
        {
            parametersPreResolved = true;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
            if (!parametersPreResolved)
                foreach (Expression parameter in parameters)
                    parameter.Resolve(generator);
        }

        List<TypeReference> parameterTypes = new List<TypeReference>();

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            foreach (Expression parameter in parameters)
            {
                if (!parameter.NeedsInference(generator, null))
                {
                    parameter.Prepare(generator, null);
                    parameterTypes.Add(parameter.TypeReference);
                }
                else
                    parameterTypes.Add(null);
            }

            TypeReference inferred = new FunctionTypeReference(this, inferredHint, parameterTypes, true);

            return parent.NeedsInference(generator, inferred);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            if (NeedsInference(generator, inferredType))
            {
                for (int i = 0; i < parameters.Count; ++i)
                {
                    if (parameterTypes[i] == null)
                    {
                        parameters[i].Prepare(generator, null);
                        parameterTypes[i] = parameters[i].TypeReference;
                    }
                }
            }

            TypeReference inferred = new FunctionTypeReference(this, inferredType, parameterTypes, true);

            parent.Prepare(generator, inferred);

            TypeReference parentType = parent.TypeReference;

            if (!parentType.IsFunction)
                throw new CompilerException(this, string.Format(Resource.Culture,
                    Resource.CannotUseExpressionOfTypeAsFunction, parent.TypeReference.TypeName.Data));

            FunctionTypeReference callType = (FunctionTypeReference)parentType;
            List<TypeReference> callParameters = callType.FunctionParameters;

            if (parameters.Count != callParameters.Count)
            {
                throw new CompilerException(this, string.Format(Resource.Culture,
                    Resource.FunctionCallParameterCountMismatch, ParameterListString(callParameters), parameters.Count));
            }

            for (int i = 0; i < parameters.Count; ++i)
            {
                if (parameterTypes[i] == null)
                {
                    parameters[i].Prepare(generator, callParameters[i]);
                    parameterTypes[i] = parameters[i].TypeReference;
                }
            }

            type = callType.ReturnType;
        }

        private string ParameterListString(System.Collections.Generic.IEnumerable<TypeReference> list)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("(");
            bool first = true;
            foreach (TypeReference tr in list)
            {
                if (first)
                    first = false;
                else
                    sb.Append(", ");
                sb.Append(tr.TypeName.Data);
            }
            sb.Append(")");
            return sb.ToString();
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);

            TypeReference parentType = parent.TypeReference;

            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);

            generator.Assembler.PushValue();

            FunctionTypeReference callType = (FunctionTypeReference)parentType;

            List<TypeReference> callParameters = callType.FunctionParameters;

            int idx = 0;
            foreach (Expression parameter in parameters)
            {
                parameter.Generate(generator);
                callParameters[idx].GenerateConversion(parameter, generator, parameter.TypeReference);
                idx++;
                generator.Assembler.PushValue();
            }

            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            Placeholder retSite = generator.Assembler.CallFromStack(parameters.Count);
            if (generator.Resolver.CurrentDefinition != null)
                generator.AddCallTraceEntry(retSite, this, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);
        }

        public override TypeReference TypeReference { get { Require.Assigned(type);  return type; } }

        public override bool HasSideEffects()
        {
            // return false when calling a generator function
            return true;
        }

        public override bool NeedsToBeStored()
        {
            // return true when calling a generator function
            // return true when calling a constructor
            if (parent is NewExpression)
                return true;
            return false;
        }
    }
}
