using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class TypeName : ILocation
    {
        private string value;
        private List<TypeName> templateParameters = new List<TypeName>();
        private int line;
        private int column;
        private string source;
        private bool isFunction;
        private TypeName returnType;
        private List<TypeName> parameters = new List<TypeName>();
        private Nullability nullability;
        private string data;
        private string dataModifierLess;
        private bool hasNamespace;

        public int Line { get { return line; } }
        public int Column { get { return column; } }
        public string Source { get { return source; } }
        public bool IsFunction { get { return isFunction; } }
        public bool IsVoid { get { return value == "void"; } }
        public bool IsDynamic { get { return value == "dynamic"; } }

        public string Data
        {
            get
            {
                if (data == null)
                {
                    StringBuilder sb = new StringBuilder();
                    PrettyPrint(sb);
                    data = sb.ToString();
                }
                return data;
            }
        }
        public string DataModifierLess
        {
            get
            {
                if (dataModifierLess == null)
                {
                    StringBuilder sb = new StringBuilder();
                    PrettyPrintNoModifiers(sb);
                    dataModifierLess = sb.ToString();
                }
                return dataModifierLess;
            }
        }

        public Nullability Nullability
        {
            get { return nullability; }
        }

        public bool Nullable
        {
            get
            {
                return nullability == Compiler.Metadata.Nullability.ExplicitNullable;
            }
        }

        public Identifier PrimaryName { get { Require.False(isFunction); return new Identifier(this, value); } }
        public bool HasNamespace { get { Require.False(isFunction); return hasNamespace; } }
        public List<TypeName> TemplateParameters { get { Require.False(isFunction); return templateParameters; } }

        public TypeName(Identifier identifier)
        {
            if (identifier == null)
                throw new ArgumentNullException("identifier");
            line = identifier.Line;
            column = identifier.Column;
            source = identifier.Source;
            value = identifier.Data;
            hasNamespace = value.Contains(".") || IsVoid;
            Require.NotEmpty(value);
        }

        private TypeName(ILocation location)
        {
            line = location.Line;
            column = location.Column;
            source = location.Source;
        }

        private TypeName(ILocation location, TypeName original)
            : this(location)
        {
            value = original.value;
            templateParameters.AddRange(original.templateParameters);
            isFunction = original.isFunction;
            returnType = original.returnType;
            parameters.AddRange(original.parameters);
            nullability = original.nullability;
            hasNamespace = original.hasNamespace;
        }

        public TypeName(Identifier identifier, Nullability nullability)
            : this(identifier)
        {
            this.nullability = nullability;
        }

        public TypeName(TypeName original, Nullability nullability)
            : this(original, original)
        {
            this.nullability = nullability;
        }

        public void SetHasNamespace()
        {
            hasNamespace = true;
        }

        public TypeName ConvertToFunction()
        {
            TypeName result = new TypeName(this);
            result.returnType = this;
            result.hasNamespace = false;
            result.isFunction = true;
            result.nullability = Compiler.Metadata.Nullability.NotNullable;
            return result;
        }

        public void AddFunctionParameter(TypeName parameter)
        {
            Require.True(isFunction);
            parameters.Add(parameter);
        }

        public void AddTemplateParameter(TypeName parameter)
        {
            Require.False(isFunction);
            templateParameters.Add(parameter);
        }

        public void PrettyPrintEscapeFunction(StringBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            if (IsFunction)
            {
                builder.Append("<");
                PrettyPrint(builder);
                builder.Append(">");
            }
            else
                PrettyPrint(builder);
        }

        private void PrettyPrintNoModifiers(StringBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            if (isFunction)
            {
                returnType.PrettyPrintEscapeFunction(builder);
                builder.Append("(");
                bool first = true;
                foreach (TypeName param in parameters)
                {
                    if (!first)
                        builder.Append(",");
                    else
                        first = false;
                    param.PrettyPrint(builder);
                }
                builder.Append(")");
            }
            else
            {
                builder.Append(value);
                if (templateParameters.Count > 0)
                {
                    builder.Append("<");
                    bool first = true;
                    foreach (TypeName param in templateParameters)
                    {
                        if (!first)
                            builder.Append(",");
                        else
                            first = false;
                        param.PrettyPrint(builder);
                    }
                    builder.Append(">");
                }
            }
        }

        public void PrettyPrint(StringBuilder builder)
        {
            PrettyPrintNoModifiers(builder);
            if (value == "void")
                return;
            bool nullable = Nullable;
            if (nullable)
                builder.Append("?");
        }

        public TypeName Prefix(string namespacePrefix)
        {
            Require.False(isFunction);
            if (!string.IsNullOrEmpty(namespacePrefix))
                namespacePrefix = namespacePrefix + ".";
            TypeName result = new TypeName(new Identifier(this, namespacePrefix + value));
            foreach (TypeName param in templateParameters)
                result.AddTemplateParameter(param);
            result.hasNamespace = true;
            return result;
        }

        public TypeName InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            if (isFunction)
            {
                TypeName result = new TypeName(this);
                result.returnType = returnType.InstantiateTemplate(parameters);
                result.isFunction = true;
                foreach (TypeName param in this.parameters)
                    result.AddFunctionParameter(param.InstantiateTemplate(parameters));
                result.nullability = nullability;
                return result;
            }
            else
            {
                if (parameters.ContainsKey(DataModifierLess))
                {
                    TypeName t = new TypeName(this, parameters[DataModifierLess]);
                    if (nullability != Compiler.Metadata.Nullability.NotNullable)
                        t.nullability = nullability;
                    return t;
                }
                if (templateParameters.Count == 0)
                    return this;
                TypeName result = new TypeName(new Identifier(this, value));
                foreach (TypeName param in templateParameters)
                    result.AddTemplateParameter(param.InstantiateTemplate(parameters));
                result.nullability = nullability;
                return result;
            }
        }

        public TypeReference ResolveFunctionType(Resolver resolver)
        {
            Require.True(isFunction);
            TypeReference returnTypeRef = resolver.ResolveType(returnType, returnType);
            List<TypeReference> parametersTypeRef = new List<TypeReference>();
            foreach (TypeName param in parameters)
                parametersTypeRef.Add(resolver.ResolveType(param, param));

            return new FunctionTypeReference(this, returnTypeRef, parametersTypeRef);
        }
    }
}
