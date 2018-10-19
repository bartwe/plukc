using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.IO;

namespace Compiler.Metadata
{
    public class Modifiers : NodeBase
    {
        private bool used;
        private bool externModifier;
        private bool staticModifier;
        private bool abstractModifier;
        private bool overrideModifier;
        private bool defaultPrivateModifier;
        private bool unusableModifier;
        private bool publicModifier;
        private bool privateModifier;
        private bool protectedModifier;
        private bool internalModifier;
        private Extern externMetadata;

        public bool Extern { get { return externModifier; } }
        public bool Static { get { return staticModifier; } }
        public bool Abstract { get { return abstractModifier; } }
        public bool Override { get { return overrideModifier; } }
        public Extern ExternMetadata { get { Require.True(Extern); return externMetadata; } }
        public bool Protected { get { return protectedModifier; } }
        public bool Private { get { return privateModifier || (defaultPrivateModifier && !(protectedModifier || publicModifier || internalModifier)); } }
        public bool Public { get { return !(Protected || Private || Internal); } }
        public bool Internal { get { return internalModifier; } }

        public Modifiers(ILocation location)
            : base(location)
        {
        }

        public Modifiers(Modifiers modifiers)
            : base(modifiers)
        {
            used = modifiers.used;
            externMetadata = modifiers.externMetadata;
            staticModifier = modifiers.staticModifier;
            abstractModifier = modifiers.abstractModifier;
            overrideModifier = modifiers.overrideModifier;
            defaultPrivateModifier = modifiers.defaultPrivateModifier;
            unusableModifier = modifiers.unusableModifier;
            publicModifier = modifiers.publicModifier;
            privateModifier = modifiers.privateModifier;
            protectedModifier = modifiers.protectedModifier;
            externMetadata = modifiers.externMetadata;
            internalModifier = modifiers.internalModifier;
        }

        public Modifiers(Modifiers modifiers, Modifiers baseModifiers)
            : base(modifiers)
        {
            used = modifiers.used;
            externMetadata = baseModifiers.externMetadata;
            staticModifier = modifiers.staticModifier || baseModifiers.staticModifier;
            abstractModifier = modifiers.abstractModifier || baseModifiers.abstractModifier;
            overrideModifier = modifiers.overrideModifier || baseModifiers.overrideModifier;
            defaultPrivateModifier = modifiers.defaultPrivateModifier || baseModifiers.defaultPrivateModifier;
            unusableModifier = modifiers.unusableModifier || baseModifiers.unusableModifier;
            publicModifier = modifiers.publicModifier || baseModifiers.publicModifier;
            privateModifier = modifiers.privateModifier || baseModifiers.privateModifier;
            protectedModifier = modifiers.protectedModifier || baseModifiers.protectedModifier;
            internalModifier = modifiers.internalModifier || baseModifiers.internalModifier;
        }

        public bool CheckVisibility(Definition definition, Definition usage)
        {
            Require.Assigned(definition);
            if (usage == null) // the compiler may use any thing from the implicit context.
                return true;
            bool fail = unusableModifier;
            if (Private)
                fail |= definition != usage;
            if (Protected)
            {
                bool match = definition == usage;
                foreach (DefinitionTypeReference dtr in usage.Extends)
                    match |= (dtr.Definition == definition);
                fail |= !match;
            }
            if (Internal)
            {
                string from = definition.Name.PrimaryName.Namespace;
                string to = usage.Name.PrimaryName.Namespace;
                if (from != to)
                {
                    if ((string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) ||
                       (!(from.StartsWith(to + ".") || to.StartsWith(from + "."))))
                        fail = true;
                }
            }
            return !fail;
        }

        public void MakeDefaultPrivate()
        {
            defaultPrivateModifier = true;
        }

        public void AddModifierPublic(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(publicModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            publicModifier = true;
        }

        public void AddModifierProtected(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(protectedModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            protectedModifier = true;
        }

        public void AddModifierPrivate(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(privateModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            privateModifier = true;
        }

        public void AddModifierInternal(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(internalModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            internalModifier = true;
        }

        public void AddModifierExtern(ILocation location, Extern externMetadata)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(externModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            externModifier = true;
            this.externMetadata = externMetadata;
        }

        public void AddModifierExtern(ILocation location)
        {
            AddModifierExtern(location, null);
        }

        public void AddModifierStatic(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(staticModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            staticModifier = true;
        }

        public void AddModifierAbstract(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(staticModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            abstractModifier = true;
        }

        public void AddModifierOverride(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            Require.False(staticModifier);
            if (!used)
                UpdateMetadataBase(location);
            used = true;
            overrideModifier = true;
        }

        public void MakeUnusable()
        {
            unusableModifier = true;
        }

        public void EnsureUnused()
        {
            Require.False(used);
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void EnsureMethodModifiers()
        {
        }

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void EnsureConstructorModifiers()
        {
            Require.False(staticModifier);
        }

        public void EnsureSlotModifiers()
        {
            if (externModifier)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.SlotModifierNotAllowed, "extern"));
            if (abstractModifier)
                throw new CompilerException(this, string.Format(Resource.Culture, Resource.SlotModifierNotAllowed, "abstract"));
        }

        public bool AllowsMethodBody()
        {
            return (!externModifier) && (!abstractModifier);
        }
    }
}
