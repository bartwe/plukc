using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public sealed class DefinitionCollection : IEnumerable<Definition>
    {
        Dictionary<string, Definition> store = new Dictionary<string, Definition>();
        Dictionary<string, Definition> templates = new Dictionary<string, Definition>();
        long typeid = 0x8000;

        public IEnumerable<Definition> Definitions { get { return store.Values; } }

        public void Add(Definition definitionMetadata)
        {
            if (definitionMetadata == null)
                throw new ArgumentNullException("definitionMetadata");
            if (store.ContainsKey(definitionMetadata.Name.DataModifierLess))
                throw new CompilerException(definitionMetadata, string.Format(Resource.Culture, Resource.TypeAlreadyDeclared, definitionMetadata.Name.Data + " (" + store[definitionMetadata.Name.DataModifierLess].Source+") "));
            if (templates.ContainsKey(definitionMetadata.Name.DataModifierLess))
                throw new CompilerException(definitionMetadata, string.Format(Resource.Culture, Resource.TypeAlreadyDeclared, definitionMetadata.Name.Data + " (" + templates[definitionMetadata.Name.DataModifierLess].Source + ") "));
            definitionMetadata.TypeReference.Id = typeid++;
            store[definitionMetadata.Name.DataModifierLess] = definitionMetadata;
        }

        public void AddTemplate(Definition definitionMetadata)
        {
            if (definitionMetadata == null)
                throw new ArgumentNullException("definitionMetadata");
            if (store.ContainsKey(definitionMetadata.Name.DataModifierLess))
                throw new CompilerException(definitionMetadata, string.Format(Resource.Culture, Resource.TypeAlreadyDeclared, definitionMetadata.Name.Data + " (" + store[definitionMetadata.Name.DataModifierLess].Source + ") "));
            if (templates.ContainsKey(definitionMetadata.Name.DataModifierLess))
                throw new CompilerException(definitionMetadata, string.Format(Resource.Culture, Resource.TypeAlreadyDeclared, definitionMetadata.Name.Data + " (" + templates[definitionMetadata.Name.DataModifierLess].Source + ") "));
            templates[definitionMetadata.Name.DataModifierLess] = definitionMetadata;
        }

        public bool HasDefinition(TypeName name)
        {
            return store.ContainsKey(name.DataModifierLess);
        }

        public Definition FindDefinition(TypeName name)
        {
            Require.True(name.HasNamespace);
            return store[name.DataModifierLess];
        }

        public bool HasTemplateDefinition(TypeName name)
        {
            Require.True(name.HasNamespace);
            return templates.ContainsKey(name.PrimaryName.Data);
        }

        public void InstantiateTemplate(TypeName name, List<TypeReference> parameters)
        {
            Require.True(HasTemplateDefinition(name));
            Require.False(HasDefinition(name));
            Definition template = templates[name.PrimaryName.Data];
            Definition result = template.InstantiateTemplate(name, parameters);
            result.TypeReference.Id = typeid++;
            if (store.ContainsKey(result.Name.DataModifierLess))
                throw new CompilerException(name, string.Format(Resource.Culture, Resource.TypeAlreadyDeclared, result.Name.Data + " (" + store[result.Name.DataModifierLess].Source + ") "));
            store[result.Name.DataModifierLess] = result;
        }

        #region IEnumerable<DefinitionMetadata> Members

        IEnumerator<Definition> IEnumerable<Definition>.GetEnumerator()
        {
            return store.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return store.Values.GetEnumerator();
        }

        #endregion
    }
}
