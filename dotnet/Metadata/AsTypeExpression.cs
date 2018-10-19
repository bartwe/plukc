using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class AsTypeExpression : PostfixExpression
    {
        Expression parent;
        TypeName type;

        TypeReference resolvedType;
        TypeReference returnType;

        public AsTypeExpression(ILocation location, TypeName type)
            : base(location)
        {
            Require.Assigned(type);
            this.type = type;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            AsTypeExpression result = new AsTypeExpression(this, type.InstantiateTemplate(parameters));
            result.SetParent(parent.InstantiateTemplate(parameters));
            return result;
        }

        public override TypeReference TypeReference
        {
            get { Require.Assigned(returnType); return returnType; }
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            resolvedType = generator.Resolver.ResolveType(type, type);
            if (!resolvedType.IsNullable)
                returnType = new NullableTypeReference(resolvedType);
            else
                returnType = resolvedType;
            parent.Resolve(generator);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            parent.Prepare(generator, resolvedType);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            TypeReference ptr = parent.TypeReference;
            TypeReference str = returnType;

            if (str.SupportsImplicit(ptr))
                returnType.GenerateConversion(this, generator, parent.TypeReference);
            else
            {
                JumpToken jtNull = null;
                jtNull = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfUnassigned(jtNull);
                generator.Assembler.TypeConversionDynamicNotNull(resolvedType.Id);
                generator.Assembler.SetDestination(jtNull);
            }
        }

        public override void SetParent(Expression parent)
        {
            Require.Assigned(parent);
            Require.Unassigned(this.parent);
            this.parent = parent;
        }

        public override bool HasSideEffects()
        {
            return parent.HasSideEffects();
        }

        public override bool NeedsToBeStored()
        {
            return parent.NeedsToBeStored();
        } 
    }
}
