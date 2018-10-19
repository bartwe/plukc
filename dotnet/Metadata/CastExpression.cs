using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class CastExpression : Expression
    {
        TypeName typeName;
        TypeReference typeReference;
        Expression parent;
        Statement throwCastException;

        public CastExpression(ILocation location, TypeName type, Expression parent)
            : base(location)
        {
            Require.Assigned(type);
            Require.Assigned(parent);
            typeName = type;
            this.parent = parent;
            throwCastException = new ThrowStatement(this, new CallExpression(this, new NewExpression(this, new TypeName(new Identifier(this, "pluk.base.CastException")))));

        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new CastExpression(
                this,
                typeName.InstantiateTemplate(parameters),
                parent.InstantiateTemplate(parameters));
        }

        public override TypeReference TypeReference
        {
            get { Require.Assigned(typeReference);  return typeReference; }
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            typeReference = generator.Resolver.ResolveType(typeName, typeName);
            parent.Resolve(generator);
            throwCastException.Resolve(generator);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            parent.Prepare(generator, typeReference);
            throwCastException.Prepare(generator);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            TypeReference ptr = parent.TypeReference;
            bool allowNull = ptr.IsNullable && !typeReference.IsNullable;
            if (!allowNull)
                generator.Assembler.CrashIfNull();
            TypeReference str = typeReference;

            if (str.SupportsImplicit(ptr))
                typeReference.GenerateConversion(this, generator, parent.TypeReference);
            else
            {
                JumpToken jtNull = null;
                if (allowNull)
                {
                    jtNull = generator.Assembler.CreateJumpToken();
                    generator.Assembler.JumpIfUnassigned(jtNull);
                }
                generator.Assembler.TypeConversionDynamicNotNull(str.Id);
                JumpToken jtOk = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfAssigned(jtOk);
                throwCastException.Generate(generator, null);
                generator.Assembler.SetDestination(jtOk);
                if (allowNull)
                    generator.Assembler.SetDestination(jtNull);
            }
        }

        public override bool HasSideEffects()
        {
            return true;
        }

        public override bool NeedsToBeStored()
        {
            return true;
        }
    }
}
