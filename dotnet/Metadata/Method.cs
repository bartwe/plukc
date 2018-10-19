using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Method : Callable
    {
        private Modifiers modifiers;
        private TypeName returnTypeName;
        private TypeReference returnType;
        private Identifier name;
        private Parameters parametersMetadata;
        private Statement statementMetadata;
        private Placeholder functionPointer;
        private List<Identifier> methodTemplateParameters;
        private CallableCastFunction castFunction;
        private bool implicitConverter;

        public Identifier Name { get { return name; } }
        public Modifiers Modifiers { get { return modifiers; } }
        public override TypeReference ReturnType { get { Require.Assigned(returnType); return returnType; } }
        public override Parameters Parameters { get { return parametersMetadata; } }
        public bool IsTemplateMethod { get { return methodTemplateParameters.Count > 0; } }
        public bool ImplicitConverter { get { return implicitConverter; } }

        public Method(ILocation location, Modifiers modifiers, TypeName returnTypeName, Identifier name, Parameters parametersMetadata, Statement statementMetadata, List<Identifier> methodTemplateParameters, bool implicitConverter)
            : base(location)
        {
            if (modifiers == null)
                throw new ArgumentNullException("modifiers");
            if (name == null)
                throw new ArgumentNullException("name");
            if (parametersMetadata == null)
                throw new ArgumentNullException("parametersMetadata");
            if (statementMetadata == null)
                throw new ArgumentNullException("statementMetadata");
            if (methodTemplateParameters == null)
                throw new ArgumentNullException("methodTemplateParameters");
            this.modifiers = modifiers;
            this.returnTypeName = returnTypeName;
            this.name = name;
            this.parametersMetadata = parametersMetadata;
            this.statementMetadata = statementMetadata;
            this.methodTemplateParameters = methodTemplateParameters;
            modifiers.EnsureMethodModifiers();
            if (!modifiers.AllowsMethodBody())
                if (!statementMetadata.IsEmptyStatement())
                    throw new CompilerException(modifiers, Resource.BodyNotCompatibleWithModifier);
            this.implicitConverter = implicitConverter;
        }

        public Method InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new Method(this, modifiers, returnTypeName.InstantiateTemplate(parameters), name,
                parametersMetadata.InstantiateTemplate(parameters), statementMetadata.InstantiateTemplate(parameters), methodTemplateParameters, implicitConverter);
        }

        private string _signature;

        public string Signature()
        {
            if (_signature == null)
            {
                StringBuilder sb = new StringBuilder();
                name.PrettyPrint(sb);
                parametersMetadata.PrettyPrintTypes(sb);
                _signature = sb.ToString();
            }
            return _signature;
        }

        private bool resolved;

        public override void Resolve(Generator generator)
        {
            Require.False(resolved);
            resolved = true;
            if (returnTypeName != null)
                returnType = generator.Resolver.ResolveType(this, returnTypeName);
            if (modifiers.Static)
                parametersMetadata.AddThisParameter(this, new StaticTypeReference(this, ParentDefinition.TypeReference));
            else
                parametersMetadata.AddThisParameter(this, ParentDefinition.TypeReference);
            parametersMetadata.Resolve(generator);
            if (!modifiers.Extern)
                statementMetadata.Resolve(generator);
            else
                if (modifiers.ExternMetadata != null)
                    modifiers.ExternMetadata.Resolve(generator);
        }

        private bool prepared;

        public override void Prepare(Generator generator, Set<TypeReference> dependsUpon)
        {
            Require.False(prepared);
            Require.True(resolved);
            prepared = true;
            ParameterMetadata thisParam = parametersMetadata.Find("this");
            if (modifiers.Static)
                Require.True(thisParam.TypeReference.IsStatic);
            dependsUpon.Put(ReturnType);
            Parameters.DependsUpon(dependsUpon);
        }

        private bool generated;

        public override void Generate(Generator generator)
        {
            Require.False(generated);
            Require.True(prepared);
            generated = true;
            castFunction = new CallableCastFunction(this);
            castFunction.Generate(generator);

            generator.Resolver.EnterContext();
            generator.Resolver.SetContextParameters(parametersMetadata);
            generator.Resolver.CurrentFieldName = name.Data + "()";

            if (modifiers.Extern)
            {
                string className = ParentDefinition.Name.PrimaryName.Data;
                if (className == "pluk.base.Array")
                {
                    string s = ParentDefinition.Name.Data;
                    if (s == "pluk.base.Array<pluk.base.Int>")
                        className = "pluk.base.Array..pluk.base.Int";
                    else if (s == "pluk.base.Array<pluk.base.Bool>")
                        className = "pluk.base.Array..pluk.base.Bool";
                    else if (s == "pluk.base.Array<pluk.base.Byte>")
                        className = "pluk.base.Array..pluk.base.Byte";
                }
                string fieldName = name.Data;
                if (modifiers.ExternMetadata == null)
                {
                    string namespaceName = ParentDefinition.Name.PrimaryName.Namespace;
                    generator.AllocateAssembler();
                    parametersMetadata.Generate(generator);
                    ParameterMetadata thisParam = parametersMetadata.Find("this");
                    generator.Resolver.SetImplicitFields(thisParam.Slot, thisParam.TypeReference);

                    generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                    generator.Assembler.StartFunction();
                    generator.Assembler.SetupNativeReturnSpace();
                    int count = parametersMetadata.NativeArgumentCount();
                    generator.Assembler.SetupNativeStackFrameArgument(count);
                    parametersMetadata.WriteNative(generator.Assembler);
                    generator.Assembler.CallNative(generator.Importer.FetchImport(namespaceName, className, fieldName), count * 2, true, false);
                    generator.Assembler.StopFunction();
                    generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
                    generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "method-externed:" + ParentDefinition.Name.Data + "." + name.Data);
                    functionPointer = generator.Assembler.Region.BaseLocation;
                }
                else
                    functionPointer = modifiers.ExternMetadata.Generate(generator, ParentDefinition.Name.PrimaryName, name, returnType, parametersMetadata);
            }
            else
            {
                generator.AllocateAssembler();
                parametersMetadata.Generate(generator);
                ParameterMetadata thisParam = parametersMetadata.Find("this");
                if (modifiers.Static)
                    Require.True(thisParam.TypeReference.IsStatic);
                generator.Resolver.SetImplicitFields(thisParam.Slot, thisParam.TypeReference);
                statementMetadata.Prepare(generator);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Assembler.StartFunction();
                JumpToken returnToken = generator.Assembler.CreateJumpToken();
                generator.Resolver.RegisterGoto("@return", returnToken);
                JumpToken recurToken = generator.Assembler.CreateJumpToken();
                generator.Assembler.SetDestination(recurToken);
                generator.Resolver.RegisterGoto("@recur", recurToken);
                statementMetadata.Generate(generator, returnType);
                if (modifiers.Abstract)
                {
                    generator.Assembler.Empty();
                    generator.Assembler.CrashIfNull();
                }
                else
                    if ((returnType != null) && (!returnType.IsVoid) && (!statementMetadata.Returns()))
                        throw new CompilerException(statementMetadata, string.Format(Resource.Culture, Resource.NotAllCodePathsReturnAValue));
                generator.Assembler.SetDestination(returnToken);
                generator.Assembler.StopFunction();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "method:" + ParentDefinition.Name.Data + "." + name.Data);
                functionPointer = generator.Assembler.Region.BaseLocation;
            }

            generator.Resolver.LeaveContext();
        }

        public Placeholder MakeMethodStruct(Generator generator, Definition definition, Placeholder definitionRuntimeStruct)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            Require.Assigned(definitionRuntimeStruct);
            Region methodStruct = generator.AllocateDataRegion();
            if (definition.GarbageCollectable)
                methodStruct.WriteNumber(1);
            else
                methodStruct.WriteNumber(0);
            methodStruct.WritePlaceholder(castFunction.FunctionPointer);
            methodStruct.WriteNumber(0);
            methodStruct.WriteNumber(0);
            methodStruct.WritePlaceholder(functionPointer);
            methodStruct.WritePlaceholder(definitionRuntimeStruct);
            StringBuilder sb = new StringBuilder(256);
            sb.Append("ms:");
            sb.Append(definition.Name.Data);
            sb.Append(":");
            sb.Append(ParentDefinition.Name.Data);
            sb.Append(".");
            sb.Append(name.Data);
            generator.Symbols.WriteData(methodStruct.BaseLocation, methodStruct.Length, sb.ToString());
            return methodStruct.BaseLocation;
        }

        public override TypeReference AsTypeReference()
        {
            return new FunctionTypeReference(this, this);
        }
    }
}
