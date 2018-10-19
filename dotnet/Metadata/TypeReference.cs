using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;

namespace Compiler.Metadata
{
    public abstract class TypeReference : NodeBase
    {
        private long id;

        protected TypeReference(ILocation location)
            : base(location)
        {
            Id = -1;
        }

        public abstract TypeName TypeName { get; }
        public long Id { get { Require.True(id != -1); return id; } set { id = value; } }

        public abstract bool IsDefinition { get; }
        public abstract bool IsFunction { get; }

        public abstract bool IsNullable { get; }
        public abstract bool IsStatic { get; }

        public abstract bool IsVoid { get; }

        public abstract void GenerateConversion(ILocation location, Generator generator, TypeReference value);
        public abstract bool Supports(TypeReference value);
        public abstract bool SupportsImplicit(TypeReference value);
        public abstract int Distance(TypeReference type);
    }

    public class DefinitionTypeReference : TypeReference
    {
        private Definition definition;

        public DefinitionTypeReference(ILocation location, Definition definition)
            : base(location)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            this.definition = definition;
        }

        public override void GenerateConversion(ILocation location, Generator generator, TypeReference value)
        {
            if (value == null)
                throw new ArgumentNullException("value");

            if (this == value)
                return;

            if (value is NullableTypeReference)
            {
                throw new CompilerException(location, string.Format(Resource.Culture,
                    Resource.IncompatibleTypes, TypeName.Data, value.TypeName.Data));
            }
            else
            {
                if (!value.IsDefinition)
                    throw new CompilerException(location, string.Format(Resource.Culture,
                        Resource.IncompatibleTypes, TypeName.Data, value.TypeName.Data));

                Definition valueDefinition = ((DefinitionTypeReference)value).Definition;
                if (valueDefinition.Supports(this))
                {
                    generator.Assembler.TypeConversion(valueDefinition.GetConversionOffset(this));
                }
                else if (valueDefinition.SupportsImplicitConversion(valueDefinition, definition))
                {
                    valueDefinition.CallImplicitConversion(generator, valueDefinition, definition);
                }
                else if (definition.SupportsImplicitConversion(valueDefinition, definition))
                {
                    definition.CallImplicitConversion(generator, valueDefinition, definition);
                }
                else
                    throw new CompilerException(location, string.Format(Resource.Culture,
                        Resource.IncompatibleTypes, TypeName.Data, value.TypeName.Data));
            }
        }

        public override bool Supports(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return false;
            else
            {
                if (!value.IsDefinition)
                    return false;
                Definition definition = ((DefinitionTypeReference)value).Definition;
                return definition.Supports(this);
            }
        }

        public override bool SupportsImplicit(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return false;
            else
            {
                if (!value.IsDefinition)
                    return false;
                Definition valueDefinition = ((DefinitionTypeReference)value).Definition;
                if (valueDefinition.Supports(this))
                    return true;
                else if (valueDefinition.SupportsImplicitConversion(valueDefinition, definition))
                {
                    return true;
                }
                else if (definition.SupportsImplicitConversion(valueDefinition, definition))
                {
                    return true;
                }
                else
                    return false;
            }
        }
        public override int Distance(TypeReference value)
        {
            if (value == null)
                return 1000;
            if (value is NullableTypeReference)
                Require.NotCalled();
            else
            {
                if (!value.IsDefinition)
                    Require.NotCalled();
                Definition definition = ((DefinitionTypeReference)value).Definition;
                return definition.SupportsDistance(this);
            }
            return 0;
        }

        public override bool IsDefinition { get { return true; } }
        public override bool IsFunction { get { return false; } }
        public override bool IsNullable { get { return false; } }
        public override bool IsStatic { get { return false; } }
        public override bool IsVoid { get { return TypeName.IsVoid; } }

        public override TypeName TypeName { get { return definition.Name; } }

        public Definition Definition
        {
            get
            {
                return definition;
            }
        }

        public Placeholder RuntimeStruct
        {
            get { return definition.RuntimeStruct; }
        }

        public override int GetHashCode()
        {
            return TypeName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            DefinitionTypeReference dtr = obj as DefinitionTypeReference;
            if (dtr == null)
                return false;
            return dtr.TypeName.Equals(TypeName);
        }

        public override string ToString()
        {
            return definition.ToString();
        }
    }

    class FunctionTypeReference : TypeReference
    {
        private TypeReference returnType;
        private List<TypeReference> parameters;
        private bool suggestion;

        public FunctionTypeReference(ILocation location, Callable callable)
            : base(location)
        {
            if (callable == null)
                throw new ArgumentNullException("callable");
            returnType = callable.ReturnType;
            parameters = new List<TypeReference>();
            foreach (ParameterMetadata pm in callable.Parameters.ParameterList)
                parameters.Add(pm.TypeReference);
        }

        public FunctionTypeReference(ILocation location, TypeReference returnType, List<TypeReference> parameters)
            : base(location)
        {
            //            if (returnType == null)
            //                throw new ArgumentNullException("returnType");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            this.returnType = returnType;
            this.parameters = parameters;
        }

        public FunctionTypeReference(ILocation location, TypeReference returnType, Parameters parameters)
            : base(location)
        {
            if (returnType == null)
                throw new ArgumentNullException("returnType");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            this.returnType = returnType;
            this.parameters = new List<TypeReference>();
            foreach (ParameterMetadata p in parameters.ParameterList)
                this.parameters.Add(p.TypeReference);
        }

        public FunctionTypeReference(ILocation location, TypeReference returnType, List<TypeReference> parameters, bool suggestion)
            : base(location)
        {
            if ((returnType == null) && !suggestion)
                throw new ArgumentNullException("returnType");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            this.returnType = returnType;
            this.parameters = parameters;
            this.suggestion = suggestion;
        }

        public override bool IsDefinition { get { return false; } }
        public override bool IsFunction { get { return true; } }
        public override bool IsNullable { get { return false; } }
        public override bool IsStatic { get { return false; } }
        public override bool IsVoid { get { return false; } }

        public bool IsSuggestion { get { return suggestion; } }

        public List<TypeReference> FunctionParameters { get { return parameters; } }

        public TypeReference ReturnType
        {
            get { return returnType; }
        }

        private TypeName typeName;

        public override TypeName TypeName
        {
            get
            {
                if (typeName == null)
                {
                    if (returnType == null)
                        typeName = new TypeName(new Identifier(new NowhereLocation(), ".")).ConvertToFunction();
                    else
                        typeName = returnType.TypeName.ConvertToFunction();
                    foreach (TypeReference tr in parameters)
                        typeName.AddFunctionParameter(tr.TypeName);
                }
                return typeName;
            }
        }

        public override void GenerateConversion(ILocation location, Generator generator, TypeReference value)
        {
            if (value is NullableTypeReference)
            {
                throw new CompilerException(location, string.Format(Resource.Culture,
                    Resource.IncompatibleTypes, TypeName.Data, value.TypeName.Data));
            }
            else
            {
                if (!this.Equals(value))
                    throw new CompilerException(location, string.Format(Resource.Culture,
                        Resource.IncompatibleTypes, TypeName.Data, value.TypeName.Data));
            }
        }

        public override bool Supports(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return false;
            FunctionTypeReference tr = value as FunctionTypeReference;
            if (tr == null)
                return false;
            if (!returnType.Supports(tr.returnType))
                return false;
            if (parameters.Count != tr.parameters.Count)
                return false;
            for (int i = 0; i < parameters.Count; ++i)
                if (!parameters[i].Supports(tr.parameters[i]))
                    return false;
            return true;
        }

        public override bool SupportsImplicit(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return false;
            FunctionTypeReference tr = value as FunctionTypeReference;
            if (tr == null)
                return false;
            if (!returnType.SupportsImplicit(tr.returnType))
                return false;
            if (parameters.Count != tr.parameters.Count)
                return false;
            for (int i = 0; i < parameters.Count; ++i)
                if (!parameters[i].SupportsImplicit(tr.parameters[i]))
                    return false;
            return true;
        }

        public override int Distance(TypeReference type)
        {
            if (type == null)
                return 1000;
            Require.True(SupportsImplicit(type));
            return 0;
        }

        public override bool Equals(object obj)
        {
            FunctionTypeReference tr = obj as FunctionTypeReference;
            if (tr == null)
                return false;
            if (!returnType.Equals(tr.returnType))
                return false;
            if (parameters.Count != tr.parameters.Count)
                return false;
            for (int i = 0; i < parameters.Count; ++i)
                if (!parameters[i].Equals(tr.parameters[i]))
                    return false;
            return true;
        }

        public override int GetHashCode()
        {
            return returnType.GetHashCode() ^ parameters.Count.GetHashCode();
        }
    }

    class NullableTypeReference : TypeReference
    {
        private TypeReference parent;
        private TypeName typeName;

        public TypeReference Parent { get { return parent; } }

        public NullableTypeReference(TypeReference parent)
            : this(parent, new TypeName(parent.TypeName, Nullability.ExplicitNullable))
        {
        }

        public NullableTypeReference(TypeReference parent, TypeName typeName)
            : base(parent)
        {
            Require.Assigned(parent);
            Require.Assigned(typeName);
            Require.True(typeName.Nullable);
            Require.False(parent is NullableTypeReference);
            Require.False(parent.TypeName.IsVoid);
            this.parent = parent;
            this.typeName = typeName;
        }

        public override TypeName TypeName
        {
            get { return typeName; }
        }

        public override bool IsDefinition
        {
            get { return false; }
        }

        public override bool IsFunction
        {
            get { return false; }
        }

        public override bool IsNullable
        {
            get { return true; }
        }

        public override bool IsStatic { get { return false; } }
        public override bool IsVoid { get { return false; } }

        public override void GenerateConversion(ILocation location, Generator generator, TypeReference value)
        {
            if (value is NullableTypeReference)
                parent.GenerateConversion(location, generator, ((NullableTypeReference)value).Parent);
            else
            {
                if (value.TypeName.Data != "void")
                    parent.GenerateConversion(location, generator, value);
            }
        }

        public override bool Supports(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return parent.Supports(((NullableTypeReference)value).Parent);
            else
            {
                if (value.TypeName.Data != "void")
                    return parent.Supports(value);
                else
                    return true;
            }
        }

        public override bool SupportsImplicit(TypeReference value)
        {
            if (value == null)
                return true;
            if (value is NullableTypeReference)
                return parent.SupportsImplicit(((NullableTypeReference)value).Parent);
            else
            {
                if (value.TypeName.Data != "void")
                    return parent.SupportsImplicit(value);
                else
                    return true;
            }
        }

        public override int Distance(TypeReference value)
        {
            if (value == null)
                return 1000;
            if (value is NullableTypeReference)
                return parent.Distance(((NullableTypeReference)value).Parent);
            else
            {
                if (value.TypeName.Data != "void")
                    return 1 + parent.Distance(value);
                else
                    return 0;
            }
        }

        public override bool Equals(object obj)
        {
            NullableTypeReference ntr = obj as NullableTypeReference;
            if (ntr == null)
                return false;
            return Parent.Equals(ntr.Parent);
        }

        public override int GetHashCode()
        {
            return Parent.GetHashCode();
        }
    }

    public class StaticTypeReference : TypeReference
    {
        private DefinitionTypeReference parent;

        public StaticTypeReference(ILocation location, DefinitionTypeReference parent)
            : base(location)
        {
            Require.Assigned(parent);
            this.parent = parent;
        }

        public DefinitionTypeReference Parent { get { return parent; } }

        public override TypeName TypeName
        {
            get { return parent.TypeName; }
        }

        public override bool IsDefinition
        {
            get { return false; }
        }

        public override bool IsFunction
        {
            get { return false; }
        }

        public override bool IsNullable
        {
            get { return false; }
        }

        public override bool IsStatic
        {
            get { return true; }
        }

        public override bool IsVoid { get { return false; } }

        public override void GenerateConversion(ILocation location, Generator generator, TypeReference value)
        {
            throw new NotImplementedException();
        }

        public override bool Supports(TypeReference value)
        {
            throw new NotImplementedException();
        }

        public override bool SupportsImplicit(TypeReference value)
        {
            throw new NotImplementedException();
        }

        public override int Distance(TypeReference type)
        {
            throw new NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            StaticTypeReference ntr = obj as StaticTypeReference;
            if (ntr == null)
                return false;
            return Parent.Equals(ntr.Parent);
        }

        public override int GetHashCode()
        {
            return Parent.GetHashCode();
        }
    }
}
