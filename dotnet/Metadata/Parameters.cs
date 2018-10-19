using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Parameters
    {
        private ParameterMetadata thisParameter;
        private List<ParameterMetadata> parameters = new List<ParameterMetadata>();
        public IList<ParameterMetadata> ParameterList { get { return parameters; } }

        public Parameters Copy()
        {
            Parameters result = new Parameters();
            result.thisParameter = thisParameter;
            result.parameters.AddRange(parameters);
            return result;
        }

        public void DependsUpon(Set<TypeReference> dependsUpon)
        {
            foreach (ParameterMetadata pm in parameters)
                dependsUpon.Put(pm.TypeReference);
        }

        public bool Same(Parameters other)
        {
            if (other.parameters.Count != parameters.Count)
                return false;
            for (int i = 0; i < parameters.Count; ++i)
            {
                if (!parameters[i].TypeReference.Equals(other.parameters[i].TypeReference))
                    return false;
            }
            return true;
        }

        public bool Match(List<TypeReference> types)
        {
            if (types.Count != parameters.Count)
                return false;
            for (int i = 0; i < parameters.Count; ++i)
            {
                if (types[i] != null)
                    if (!parameters[i].TypeReference.SupportsImplicit(types[i]))
                        return false;
            }
            return true;
        }

        public int CompareTo(Parameters parameters, List<TypeReference> types)
        {
            for (int i = 0; i < parameters.ParameterList.Count; i++)
            {
                TypeReference self = this.parameters[i].TypeReference;
                TypeReference other = parameters.ParameterList[i].TypeReference;
                int s = self.Distance(types[i]);
                int o = other.Distance(types[i]);
                int c = o.CompareTo(s);
                if (c != 0)
                    return c;
            }
            return 0;
        }

        public Parameters InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Parameters result = new Parameters();
            if (thisParameter != null)
                result.thisParameter = thisParameter.InstantiateTemplate(parameters);
            foreach (ParameterMetadata parameter in this.parameters)
                result.parameters.Add(parameter.InstantiateTemplate(parameters));
            return result;
        }

        public void AddParameter(ILocation location, TypeName type, Identifier name)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            foreach (ParameterMetadata param in parameters)
                Require.False(param.Name == name.Data);
            parameters.Add(new ParameterMetadata(location, type, name));
        }

        public ParameterMetadata AddParameter(ILocation location, TypeReference type, Identifier name)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            foreach (ParameterMetadata param in parameters)
                Require.False(param.Name == name.Data);
            ParameterMetadata pm = new ParameterMetadata(location, type, name);
            parameters.Add(pm);
            return pm;
        }

        public void PrettyPrint(StringBuilder builder)
        {
            builder.Append('(');
            IEnumerator<ParameterMetadata> enumerator = parameters.GetEnumerator();
            bool busy = enumerator.MoveNext();
            while (busy)
            {
                enumerator.Current.PrettyPrint(builder);
                busy = enumerator.MoveNext();
                if (busy)
                    builder.Append(", ");
            }
            builder.Append(')');
        }

        public void PrettyPrintTypes(StringBuilder builder)
        {
            builder.Append('(');
            IEnumerator<ParameterMetadata> enumerator = parameters.GetEnumerator();
            bool busy = enumerator.MoveNext();
            while (busy)
            {
                enumerator.Current.PrettyPrintType(builder);
                busy = enumerator.MoveNext();
                if (busy)
                    builder.Append(", ");
            }
            builder.Append(')');
        }

        public void AddThisParameter(ILocation location, TypeReference type)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            Require.Unassigned(thisParameter);
            thisParameter = new ParameterMetadata(location, type, new Identifier(location, "this"));
        }

        public void Resolve(Generator generator)
        {
            if (thisParameter != null)
                thisParameter.Resolve(generator);
            foreach (ParameterMetadata parameter in parameters)
                parameter.Resolve(generator);
        }

        public void Generate(Generator generator)
        {
            if (thisParameter != null)
                thisParameter.Generate(generator);
            foreach (ParameterMetadata parameter in parameters)
                parameter.Generate(generator);
        }

        public int NativeArgumentCount()
        {
            int pc = parameters.Count;
            if (thisParameter != null)
                pc++;
            return pc;
        }

        public void WriteNative(Assembler assembler)
        {
            //push arguments in reverse order like c likes
            int count = 0;
            List<ParameterMetadata> rev = new List<ParameterMetadata>(parameters);
            rev.Reverse();
            int pc = rev.Count;
            if (thisParameter != null)
                pc++;
            foreach (ParameterMetadata parameter in rev)
                parameter.WriteNative(assembler, count++, pc);
            if (thisParameter != null)
                thisParameter.WriteNative(assembler, count++, pc);
        }

        public ParameterMetadata Find(string name)
        {
            if (name == "this")
                return thisParameter;
            foreach (ParameterMetadata parameter in parameters)
                if (parameter.Name == name)
                    return parameter;
            return null;
        }
    }

    public class ParameterMetadata : NodeBase
    {
        private Identifier name;
        private TypeName typeName;
        private TypeReference type;
        private int slot = int.MinValue;

        public string Name { get { return name.Data; } }
        public TypeReference TypeReference { get { Require.Assigned(type); return type; } }
        public int Slot { get { return slot; } }

        public ParameterMetadata(ILocation location, TypeName type, Identifier name)
            : base(location)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (name == null)
                throw new ArgumentNullException("name");
            this.typeName = type;
            this.name = name;
        }

        public ParameterMetadata(ILocation location, TypeReference type, Identifier name)
            : base(location)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (name == null)
                throw new ArgumentNullException("name");
            this.type = type;
            this.name = name;
        }

        public ParameterMetadata InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Require.Assigned(typeName);
            return new ParameterMetadata(this, typeName.InstantiateTemplate(parameters), name);
        }

        public void PrettyPrint(StringBuilder builder)
        {
            if (type == null)
                typeName.PrettyPrint(builder);
            else
                type.TypeName.PrettyPrint(builder);
            builder.Append(' ');
            name.PrettyPrint(builder);
        }

        public void PrettyPrintType(StringBuilder builder)
        {
            if (type == null)
                typeName.PrettyPrint(builder);
            else
                type.TypeName.PrettyPrint(builder);
        }

        public void Resolve(Generator generator)
        {
            if (type == null)
                type = generator.Resolver.ResolveType(this, typeName);
        }

        public void Generate(Generator generator)
        {
            slot = generator.Assembler.AddParameter();
            generator.Resolver.AddVariable(name, type, slot, name.Data == "this");
            generator.Resolver.AssignSlot(slot);
            generator.Resolver.RetrieveSlot(this, slot, false); // treat function arguments as used
        }

        public void Bind(int slot)
        {
            this.slot = slot;
        }

        public void WriteNative(Assembler assembler, int index, int count)
        {
            assembler.SetNativeArgument(slot, index, count);
        }
    }
}
