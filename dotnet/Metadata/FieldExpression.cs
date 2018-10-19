using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class FieldExpression : PostfixExpression, IAssignableExpression, IPossibleTypeName
    {
        private Expression parent;
        private Identifier name;
        private TypeReference fieldType;
        private bool skipGenerateParent;
        private Method method;
        bool allowTypeGenerate;
        private bool hasTypeName;

        private StaticTypeReference typeName;
        private bool sideEffects;

        public FieldExpression(ILocation location, Identifier name)
            : base(location)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            this.name = name;
        }

        public FieldExpression(ILocation location, Expression parent, Identifier name)
            : this(location, name)
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

        public void SetParentDoNotGenerate(Expression expression)
        {
            Require.Unassigned(parent);
            this.parent = expression;
            this.skipGenerateParent = true;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new FieldExpression(this, parent.InstantiateTemplate(parameters), name);
        }

        public Expression ConvertToAssignment(ILocation location, Expression value)
        {
            return new AssignmentExpression(location, parent, name, value);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (!skipGenerateParent)
                parent.Resolve(generator);
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
            if (!skipGenerateParent)
                parent.Prepare(generator, null);
            if (hasTypeName)
                return false;
            TypeReference type = parent.TypeReference;
            bool staticRef = type.IsStatic;
            if (staticRef)
                type = ((StaticTypeReference)type).Parent;
            if (type.IsDefinition)
            {
                Definition definition = ((DefinitionTypeReference)type).Definition;
                if ((!definition.HasField(name, staticRef)) && (definition.HasMethod(name, staticRef)))
                {
                    return definition.FindMethod(name, staticRef, inferredHint, null, false) == null;
                }
            }
            return false;
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            NeedsInference(generator, inferredType);
            if (UseTypeName())
            {
                fieldType = typeName;
            }
            else
            {
                TypeReference type = parent.TypeReference;
                bool failed = false;
                bool foundButNotStatic = false;
                bool static_ = type.IsStatic;
                bool foundStatic = false;
                if (static_)
                    type = ((StaticTypeReference)type).Parent;
                if (type.IsNullable)
                    throw new CompilerException(this, string.Format(Resource.Culture, Resource.FieldFromNullableType, name.Data, type.TypeName.Data));
                if (type.IsDefinition)
                {
                    Definition definition = ((DefinitionTypeReference)type).Definition;
                    if (definition.HasField(name))
                    {
                        foundButNotStatic = true;
                        Field field = definition.GetField(name, static_);
                        foundStatic = field.GetModifiers.Static;
                        if (static_ && !field.GetModifiers.Static)
                            failed = true;
                        else
                            fieldType = field.TypeReference;
                    }
                    else if (definition.HasMethod(name))
                    {
                        foundButNotStatic = true;
                        method = definition.FindMethod(name, static_, inferredType, generator.Resolver.CurrentDefinition, true);
                        foundStatic = method.Modifiers.Static;
                        if (static_ && !method.Modifiers.Static)
                            failed = true;
                        else
                            fieldType = method.AsTypeReference();
                    }
                    else if (definition.HasProperty(name))
                    {
                        foundButNotStatic = true;
                        Property property = definition.GetProperty(name);
                        foundStatic = property.GetModifiers.Static && property.SetModifiers.Static;
                        if (static_ && (!property.GetModifiers.Static || !property.SetModifiers.Static))
                            failed = true;
                        else
                        {
                            fieldType = property.ReturnType;
                            sideEffects = true;
                        }
                    }
                    else
                        failed = true;
                }
                else
                    failed = true;

                if (foundStatic)
                    if (parent is IIncompleteSlotAssignment)
                        ((IIncompleteSlotAssignment)parent).AllowRetrieval();

                if (failed)
                {
                    if (foundButNotStatic)
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveStaticFieldExpression, name.Data, type.TypeName.Data));
                    else
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.FailedToResolveFieldExpression, name.Data, type.TypeName.Data));
                }
            }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            if ((typeName != null) && UseTypeName())
            {
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Assembler.SetImmediateValue(typeName.Parent.RuntimeStruct, 0);
                if (!allowTypeGenerate)
                    throw new CompilerException(this, Resource.CannotUseATypeAsAValue);
            }
            else
            {
                TypeReference type;
                if (parent.SuppliesType())
                {
                    type = parent.TypeReference;
                    bool static_ = type.IsStatic;
                    if (static_)
                        parent.ConsumeType();
                }
                if (!skipGenerateParent)
                    parent.Generate(generator);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                type = parent.TypeReference;
                bool staticRef = type.IsStatic;
                if (staticRef)
                    type = ((StaticTypeReference)type).Parent;
                if (type.IsDefinition)
                {
                    Definition definition = ((DefinitionTypeReference)type).Definition;
                    if (definition.HasField(name, staticRef))
                    {
                        Field field = definition.GetField(name, staticRef);
                        generator.Resolver.RetrieveField(this, field);
                        if (!field.GetModifiers.Static)
                        {
                            int offset = definition.GetFieldOffset(this, name, generator.Resolver.CurrentDefinition, false);
                            generator.Assembler.FetchField(offset);
                        }
                        else
                        {
                            generator.Assembler.Load(field.StaticSlot);
                        }
                    }
                    else if (definition.HasMethod(name, staticRef))
                    {
                        int offset = definition.GetMethodOffset(this, method, generator.Resolver.CurrentDefinition);
                        generator.Assembler.FetchMethod(offset);
                    }
                    else if (definition.HasProperty(name, staticRef))
                    {
                        int offset = definition.GetGetPropertyOffset(this, name, generator.Resolver.CurrentDefinition);
                        generator.Assembler.FetchMethod(offset);
                        generator.Assembler.PushValue();
                        Placeholder retSite = generator.Assembler.CallFromStack(0);
                        generator.AddCallTraceEntry(retSite, this, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);
                    }
                }
            }
        }

        public override TypeReference TypeReference { get { Require.Assigned(fieldType); return fieldType; } }

        public bool IsPossibleTypeName()
        {
            if (skipGenerateParent)
                return false;
            if (parent is IPossibleTypeName)
                return ((IPossibleTypeName)parent).IsPossibleTypeName();
            return false;
        }

        public Identifier GetTypeIdentifier()
        {
            Identifier i = ((IPossibleTypeName)parent).GetTypeIdentifier();
            return new Identifier(i, i.Data + "." + name.Data);
        }

        public void HasTypeName()
        {
            hasTypeName = true;
            ((IPossibleTypeName)parent).HasTypeName();
        }

        public bool UseTypeName()
        {
            return hasTypeName && (parent is IPossibleTypeName) && ((IPossibleTypeName)parent).UseTypeName();
        }

        internal protected override void ConsumeType()
        {
            allowTypeGenerate = true;
        }

        internal protected override bool SuppliesType()
        {
            return (typeName != null) && UseTypeName();
        }

        public override bool HasSideEffects()
        {
            return sideEffects;
        }
    }
}
