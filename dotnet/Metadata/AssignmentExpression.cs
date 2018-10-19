using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class AssignmentExpression : Expression
    {
        private Expression target;
        private Identifier name;
        private Expression value;
        private TypeReference type;

        public AssignmentExpression(ILocation location, Expression target, Identifier name, Expression value)
            : base(location)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            if (value == null)
                throw new ArgumentNullException("value");
            this.target = target;
            this.name = name;
            this.value = value;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            if (target == null)
                return new AssignmentExpression(this, null, name, value.InstantiateTemplate(parameters));
            else
                return new AssignmentExpression(this, target.InstantiateTemplate(parameters),
                    name, value.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (target != null)
                target.Resolve(generator);
            value.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            if (target == null)
            {
                Resolver.ImplicitField implicitSlot = generator.Resolver.FindImplicitField(name);
                if (implicitSlot != null)
                {
                    target = new DirectSlotExpression(this, implicitSlot.slot, implicitSlot.type);
                    target.Resolve(generator);
                }
            }
            if (target == null)
            {
                type = generator.Resolver.ResolveSlotType(name);
                Require.Assigned(type);
                value.Prepare(generator, type);
                return false;
            }
            else
                return target.NeedsInference(generator, inferredHint);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            NeedsInference(generator, inferredType); // also inits the 'target' variable
            if (target != null)
            {
                target.Prepare(generator, inferredType);

                TypeReference thisType = target.TypeReference;

                bool static_ = thisType.IsStatic;
                if (static_)
                    thisType = ((StaticTypeReference)thisType).Parent;

                Definition thisDefinition = null;
                if (thisType is DefinitionTypeReference)
                    thisDefinition = ((DefinitionTypeReference)thisType).Definition;

                if (!(thisType.IsDefinition && (thisDefinition.HasField(name, static_) || thisDefinition.HasProperty(name, static_))))
                {
                    if ((target is SlotExpression) && (target as SlotExpression).IsThis)
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveVariable, name.Data));
                    else
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveFieldExpression, name.Data, thisType.TypeName.Data));
                }

                if (thisDefinition.HasField(name, static_))
                {
                    Field field = thisDefinition.GetField(name, static_);
                    if (static_ && !field.GetModifiers.Static)
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveStaticFieldExpression, name.Data, thisDefinition.Name.DataModifierLess));
                    type = field.TypeReference;
                    value.Prepare(generator, type);
                    if (target is IIncompleteSlotAssignment)
                        ((IIncompleteSlotAssignment)target).AllowRetrieval();
                }
                else
                {
                    Property property = thisDefinition.GetProperty(name);
                    if (static_ && !(property.SetModifiers.Static && property.GetModifiers.Static))
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveStaticFieldExpression, name.Data, thisDefinition.Name.DataModifierLess));
                    type = property.ReturnType;
                    value.Prepare(generator, type);
                }
            }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            if (target == null)
            {
                int slot = generator.Resolver.ResolveSlotOffset(name);
                value.Generate(generator);
                type.GenerateConversion(this, generator, value.TypeReference);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Resolver.WriteSlot(this, slot);
                generator.Assembler.StoreVariable(slot);
            }
            else
            {
                target.Generate(generator);

                TypeReference thisType = target.TypeReference;

                bool staticRef = thisType is StaticTypeReference;

                Definition thisDefinition;
                if (staticRef)
                    thisDefinition = ((StaticTypeReference)thisType).Parent.Definition;
                else
                    thisDefinition = ((DefinitionTypeReference)thisType).Definition;

                if (thisDefinition.HasField(name, staticRef))
                {
                    Field field = thisDefinition.GetField(name, staticRef);
                    if (!field.GetModifiers.Static)
                    {
                        generator.Assembler.PushValue(); 
                        int fieldOffset = thisDefinition.GetFieldOffset(this, name, generator.Resolver.CurrentDefinition, true);
                        if (target is IIncompleteSlotAssignment)
                            if (((IIncompleteSlotAssignment)target).IsIncompleteSlot())
                            {
                                generator.Resolver.AssignField(field);
                            }
                        value.Generate(generator);
                        type.GenerateConversion(value, generator, value.TypeReference);
                        generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                        TypeReference slot = field.TypeReference;
                        if (slot.IsNullable)
                            slot = ((NullableTypeReference)slot).Parent;
                        DefinitionTypeReference dtr = slot as DefinitionTypeReference;
                        if ((dtr != null) && (!dtr.Definition.GarbageCollectable))
                            generator.Assembler.StoreInFieldOfSlotNoTouch(fieldOffset);
                        else
                            generator.Assembler.StoreInFieldOfSlot(generator.Toucher, fieldOffset);
                    }
                    else
                    {
                        generator.Assembler.PushValue();
                        if (target is IIncompleteSlotAssignment)
                            if (((IIncompleteSlotAssignment)target).IsIncompleteSlot())
                            {
                                generator.Resolver.AssignField(field);
                            }
                        value.Generate(generator);
                        type.GenerateConversion(value, generator, value.TypeReference);
                        generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                        generator.Assembler.Store(field.StaticSlot);
                    }
                }
                else
                {
                    int propertySlot = thisDefinition.GetSetPropertyOffset(this, name, generator.Resolver.CurrentDefinition);
                    generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                    generator.Assembler.FetchMethod(propertySlot);
                    generator.Assembler.PushValue();
                    value.Generate(generator);
                    type.GenerateConversion(value, generator, value.TypeReference);
                    generator.Assembler.PushValue();
                    Placeholder retSite = generator.Assembler.CallFromStack(1);
                    generator.AddCallTraceEntry(retSite, this, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);
                }
            }
        }

        public override TypeReference TypeReference { get { Require.Assigned(type); return type; } }

        public override bool HasSideEffects()
        {
            return true;
        }

        public override bool NeedsToBeStored()
        {
            return false;
        }
    }
}
