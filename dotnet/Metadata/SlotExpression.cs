using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class SlotExpression : Expression, IAssignableExpression, IPossibleTypeName, IIncompleteSlotAssignment
    {
        private Identifier name;
        private TypeReference type;

        private FieldExpression field;
        private bool hasTypeName;
        private bool useTypeName;
        private StaticTypeReference typeName;
        private bool sideEffects;

        public bool IsThis { get { return name.Data == "this"; } }
        private bool allowIncomplete;

        public SlotExpression(ILocation location, Identifier name, bool allowIncomplete)
            : base(location)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            this.name = name;
            this.allowIncomplete = allowIncomplete;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new SlotExpression(this, name, allowIncomplete);
        }

        public Expression ConvertToAssignment(ILocation location, Expression value)
        {
            return new AssignmentExpression(location, null, name, value);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (IsPossibleTypeName())
            {
                TypeReference t = generator.Resolver.TryResolveType(new TypeName(GetTypeIdentifier()));
                if ((t != null) && t.IsDefinition)
                {
                    typeName = new StaticTypeReference(this, (DefinitionTypeReference)t);
                    HasTypeName();
                }
            }
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            Resolver.ImplicitField implicitSlot = generator.Resolver.FindImplicitField(name);
            if (implicitSlot != null)
            {
                Expression thisSlot = new DirectSlotExpression(this, implicitSlot.slot, implicitSlot.type);
                FieldExpression field = new FieldExpression(this, thisSlot, name);
                field.Resolve(generator);
                return field.NeedsInference(generator, inferredHint);
            }
            return false;
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            Resolver.ImplicitField implicitSlot = generator.Resolver.FindImplicitField(name);
            if (implicitSlot != null)
            {
                Expression thisSlot = new DirectSlotExpression(this, implicitSlot.slot, implicitSlot.type);
                field = new FieldExpression(this, thisSlot, name);
                field.Resolve(generator);
                field.Prepare(generator, inferredType);
                type = field.TypeReference;
                sideEffects = field.HasSideEffects();
            }
            else
            {
                if (generator.Resolver.ContainsSlot(name))
                    type = generator.Resolver.ResolveSlotType(name);
                else if (!hasTypeName)
                {
                    throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveVariable, name.Data));
                }
                else
                {
                    if (typeName != null)
                        type = typeName; //todo
                    useTypeName = true;
                }
            }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            if (UseTypeName())
            {
                generator.Assembler.SetImmediateValue(typeName.Parent.RuntimeStruct, 0);
            }
            else
            {
                if (field == null)
                {
                    int slot = generator.Resolver.ResolveSlotOffset(name);
                    generator.Resolver.RetrieveSlot(this, slot, allowIncomplete);
                    generator.Assembler.RetrieveVariable(slot);
                }
                else
                {
                    field.Generate(generator);
                }
            }
        }

        public override TypeReference TypeReference { get { Require.Assigned(type); return type; } }

        public bool IsPossibleTypeName()
        {
            return (!IsThis) && (!name.Data.StartsWith("{"));
        }

        public Identifier GetTypeIdentifier()
        {
            return name;
        }

        public void HasTypeName()
        {
            hasTypeName = true;
        }

        public bool UseTypeName()
        {
            return useTypeName;
        }

        public override bool NeedsToBeStored()
        {
            return false;
        }

        public override bool HasSideEffects()
        {
            return sideEffects;
        }

        public void AllowRetrieval()
        {
            allowIncomplete = true;
        }

        public bool IsIncompleteSlot()
        {
            return allowIncomplete;
        }
    }
}
