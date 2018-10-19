using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class NewExpression : Expression
    {
        private bool inferred;
        private TypeName typeName;
        private DefinitionTypeReference type; // the type instantiated, not the type of the constructor
        private Constructor constructor;

        public NewExpression(ILocation location, TypeName typeName)
            : base(location)
        {
            if (typeName == null)
                throw new ArgumentNullException("typeName");
            this.typeName = typeName;
        }

        public NewExpression(ILocation location)
            : base(location)
        {
            inferred = true;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            if (inferred)
                return new NewExpression(this);
            else
                return new NewExpression(this, typeName.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (!inferred)
            {
                Require.Unassigned(type);
                type = generator.Resolver.ResolveDefinitionType(typeName, typeName);
            }
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            if (!inferred)
                return false;
            TypeReference suggestion = inferredHint;
            if ((suggestion != null) && (suggestion.IsFunction))
                suggestion = ((FunctionTypeReference)suggestion).ReturnType;
            else
                suggestion = null;
            suggestion = suggestion as DefinitionTypeReference;
            return (suggestion == null);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            TypeReference suggestion = inferredType;
            if ((suggestion != null) && (suggestion.IsFunction))
                suggestion = ((FunctionTypeReference)suggestion).ReturnType;
            if ((suggestion != null) && (suggestion.IsNullable))
                suggestion = ((NullableTypeReference)suggestion).Parent;
            if (inferred)
                type = suggestion as DefinitionTypeReference;
            if (type == null)
            {
                if (suggestion == null)
                    throw new CompilerException(this, Resource.TypeOfExpressionUnclear);
                else
                    throw new CompilerException(this, string.Format(Resource.Culture,
                        Resource.CanOnlyCreateNewInstanceOfClass, suggestion.TypeName.Data));
            }
            else
                if (type.Definition.Modifiers.Abstract)
                    throw new CompilerException(this, string.Format(Resource.Culture,
                        Resource.CannotCreateInstanceOfAbstractClass, type.Definition.Name.Data));
            constructor = type.Definition.FindConstructor(this, inferredType, generator.Resolver.CurrentDefinition);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.LoadMethodStruct(constructor.RuntimeStruct);
        }

        public override TypeReference TypeReference
        {
            get
            {
                Require.Assigned(constructor);
                return constructor.AsTypeReference();
            }
        }
    }
}
