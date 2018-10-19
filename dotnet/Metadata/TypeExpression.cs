using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class TypeExpression : Expression
    {
        TypeName typeName;
        StaticTypeReference type;
        bool allowGenerate;

        public TypeExpression(ILocation location, TypeName typeName)
            : base(location)
        {
            if (typeName == null)
                throw new ArgumentNullException("typeName");
            this.typeName = typeName;
        }

        public TypeExpression(ILocation location, StaticTypeReference type)
            : base(location)
        {
            Require.Assigned(type);
            typeName = type.TypeName;
            this.type = type;
            allowGenerate = true;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            type = new StaticTypeReference(this, generator.Resolver.ResolveDefinitionType(typeName, typeName));
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.SetImmediateValue(type.Parent.RuntimeStruct, 0);
            if (!allowGenerate)
                throw new CompilerException(this, Resource.CannotUseATypeAsAValue);
        }

        public override TypeReference TypeReference { get { Require.Assigned(type); return type; } }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new TypeExpression(this, typeName.InstantiateTemplate(parameters));
        }

        internal protected override bool SuppliesType()
        {
            return true;
        }

        internal protected override void ConsumeType()
        {
            allowGenerate = true;
        }
    }
}
