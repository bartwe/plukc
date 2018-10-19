using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Field : NodeBase
    {
        private Modifiers getModifiers;
        private Modifiers setModifiers;
        private Identifier name;
        private TypeName typeName;
        private TypeReference type;
        private Definition parentDefinition;

        private Placeholder staticStorage;

        public Identifier Name { get { return name; } }
        public string LocalName
        {
            get
            {
                if (GetModifiers.Private && SetModifiers.Private)
                    return parentDefinition.Name.Data + ":" + Name.Data;
                return Name.Data;
            }
        }
        public Modifiers GetModifiers { get { return getModifiers; } }
        public Modifiers SetModifiers { get { return setModifiers; } }
        public TypeReference TypeReference { get { Require.Assigned(type); return type; } }

        public Placeholder StaticSlot { get { Require.Assigned(staticStorage); return staticStorage; } }

        public Field(ILocation location, Modifiers getModifier, Modifiers setModifier, TypeName typeName, Identifier name)
            : base(location)
        {
            if (getModifier == null)
                throw new ArgumentNullException("getModifier");
            if (setModifier == null)
                throw new ArgumentNullException("setModifier");
            if (typeName == null)
                throw new ArgumentNullException("typeName");
            if (name == null)
                throw new ArgumentNullException("name");
            this.getModifiers = getModifier;
            this.setModifiers = setModifier;
            this.typeName = typeName;
            this.name = name;
            getModifier.EnsureSlotModifiers();
            setModifier.EnsureSlotModifiers();
        }

        public Definition ParentDefinition { get { Require.Assigned(parentDefinition); return parentDefinition; } }

        public void SetParentDefinition(Definition parentDefinition)
        {
            if (parentDefinition == null)
                throw new ArgumentNullException("parentDefinition");
            this.parentDefinition = parentDefinition;
        }

        public Field InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new Field(this, getModifiers, setModifiers, typeName.InstantiateTemplate(parameters), name);
        }

        private bool resolved;

        public void Resolve(Generator generator)
        {
            Require.False(resolved);
            resolved = true;
            type = generator.Resolver.ResolveType(this, typeName);
        }

        private bool prepared;

        public void Prepare(Generator generator, Set<TypeReference> dependsUpon)
        {
            Require.True(resolved);
            Require.False(prepared);
            prepared = true;
            dependsUpon.Put(type);
        }

        public bool CheckAssigned(Resolver resolver)
        {
            if (type is NullableTypeReference)
                return true;
            return resolver.IsFieldAssigned(this);
        }

        public void PrepareStatic(Generator generator, Set<TypeReference> dependsUpon)
        {
            Require.True(GetModifiers.Static);
            dependsUpon.Put(type);
            staticStorage = generator.Statics.CurrentLocation;
            generator.Statics.WriteNumber(0);
            generator.Statics.WriteNumber(0);
        }
    }
}
