using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Constructor : Callable
    {
        private Modifiers modifiers;
        private Parameters parametersMetadata;
        private Statement statementMetadata;
        private Placeholder functionPointer;
        private CallableCastFunction castFunction;
        private List<Expression> anotherConstructor;
        private ILocation anotherConstructorLocation;
        private Constructor anotherConstructorInstance;
        private Region runtimeStruct;
        private List<BaseConstructor> baseConstructors = new List<BaseConstructor>();
        private ConstructorNode rootConstructorNode;

        public Placeholder RuntimeStruct { get { return runtimeStruct.BaseLocation; } }
        public override TypeReference ReturnType { get { return ParentDefinition.TypeReference; } }
        public override Parameters Parameters { get { return parametersMetadata; } }
        public Modifiers Modifiers { get { return modifiers; } }

        public Constructor(ILocation location, Modifiers modifiers, Parameters parameters)
            : base(location)
        {
            if (modifiers == null)
                throw new ArgumentNullException("modifiers");
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            this.modifiers = modifiers;
            this.parametersMetadata = parameters;
            modifiers.EnsureConstructorModifiers();
        }

        public void SetBody(Statement statement)
        {
            if (statement == null)
                throw new ArgumentNullException("statement");
            this.statementMetadata = statement;
            if (!modifiers.AllowsMethodBody())
                if (statementMetadata.GetType() != typeof(Compiler.Metadata.EmptyStatement))
                    throw new CompilerException(modifiers, Resource.BodyNotCompatibleWithModifier);
        }

        public void CallAnotherConstructor(ILocation location, List<Expression> arguments)
        {
            anotherConstructorLocation = location;
            anotherConstructor = arguments;
        }

        public void CallBaseConstructor(TypeName type, List<Expression> arguments)
        {
            BaseConstructor t = new BaseConstructor();
            t.type = type;
            t.arguments = arguments;
            baseConstructors.Add(t);
        }

        public Constructor InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Constructor result = new Constructor(this, modifiers, parametersMetadata.InstantiateTemplate(parameters));
            if (anotherConstructor != null)
            {
                List<Expression> arguments = new List<Expression>();
                foreach (Expression a in anotherConstructor)
                    arguments.Add(a.InstantiateTemplate(parameters));
                result.CallAnotherConstructor(anotherConstructorLocation, arguments);
            }
            foreach (BaseConstructor c in baseConstructors)
            {
                List<Expression> arguments = new List<Expression>();
                foreach (Expression a in c.arguments)
                    arguments.Add(a.InstantiateTemplate(parameters));
                result.CallBaseConstructor(c.type.InstantiateTemplate(parameters), arguments);
            }
            result.SetBody(statementMetadata.InstantiateTemplate(parameters));
            return result;
        }

        private string _constructorName;

        public string ConstructorName()
        {
            if (_constructorName == null)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("this");
                parametersMetadata.PrettyPrint(builder);
                _constructorName = builder.ToString();
            }
            return _constructorName;
        }

        public override void Resolve(Generator generator)
        {
            parametersMetadata.AddThisParameter(this, ParentDefinition.TypeReference);
            parametersMetadata.Resolve(generator);

            if (anotherConstructor != null)
                foreach (Expression a in anotherConstructor)
                    a.Resolve(generator);

            foreach (BaseConstructor c in baseConstructors)
            {
                if (c.type != null)
                    c.typeReference = generator.Resolver.ResolveDefinitionType(this, c.type);
            }
            foreach (BaseConstructor c in baseConstructors)
                c.Resolve(generator);
            statementMetadata.Resolve(generator);
        }

        public override void Prepare(Generator generator, Set<TypeReference> dependsUpon)
        {
            if (anotherConstructor != null)
            {
                List<TypeReference> signature = new List<TypeReference>();
                foreach (Expression a in anotherConstructor)
                    signature.Add(null);
                //                signature.Add(a.TypeReference); TODO
                anotherConstructorInstance = ParentDefinition.FindConstructor(anotherConstructorLocation, new FunctionTypeReference(this, ParentDefinition.TypeReference, signature), generator.Resolver.CurrentDefinition);
            }
            foreach (BaseConstructor c in baseConstructors)
                if (c.type == null)
                {
                    List<DefinitionTypeReference> se = ParentDefinition.SparseExtends;
                    if ((baseConstructors.Count != 1) || (se.Count != 1))
                        throw new CompilerException(this, string.Format(Resource.Culture, Resource.ImplicitConstructor));
                    c.typeReference = se[0];
                }
            Parameters.DependsUpon(dependsUpon);
            runtimeStruct = generator.AllocateDataRegion();
        }

        public Placeholder MakeRuntimeStruct(Generator generator, Dictionary<Definition, Placeholder> implicitConversions)
        {
            rootConstructorNode.MakeRuntimeStruct(generator, implicitConversions);
            return RuntimeStruct;
        }

        public void PrepareNestedConstructors(Generator generator)
        {
            Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> irc = InnerResolveConstructor();
            rootConstructorNode = InnerPrepareConstructors(generator, irc, ParentDefinition);
        }

        private int BaseConstructorsSorter(BaseConstructor l, BaseConstructor r)
        {
            return ParentDefinition.Extends.IndexOf(l.typeReference).CompareTo(ParentDefinition.Extends.IndexOf(r.typeReference));
        }

        private Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> InnerResolveConstructor()
        {
            // the key is the type to be constructed
            // the value is the definition that has the constructor to be used for that type
            if (anotherConstructorInstance != null)
                return anotherConstructorInstance.InnerResolveConstructor();

            // 1 sort explicit constructors to extends order (which is ordered in a way required for correct dependencies (and initialisation), assumpsions)
            // most primary first (order of the extends list)
            baseConstructors.Sort(BaseConstructorsSorter);

            // 2 add the explicit constructors to the mapping directly
            Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> result = new Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>>();
            foreach (BaseConstructor b in baseConstructors)
            {
                result.Add(b.typeReference, new KeyValuePair<Definition, List<Expression>>(ParentDefinition, b.arguments));
            }

            // 3 walk the explicit constructors, add the results to the mapping if not allready override
            // todo allow collisions if both are the empty parameter constructor
            Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> nested = new Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>>();
            foreach (BaseConstructor b in baseConstructors)
            {
                List<TypeReference> signature = new List<TypeReference>();
                foreach (Expression a in b.arguments)
                    signature.Add(null); // TODO: prepare the expression if possible
                //                    signature.Add(a.TypeReference);
                Constructor c = ((DefinitionTypeReference)b.typeReference).Definition.FindConstructor(b.type, new FunctionTypeReference(this, b.typeReference, signature), ParentDefinition);
                foreach (KeyValuePair<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> m in c.InnerResolveConstructor())
                {
                    if (result.ContainsKey(m.Key))
                        continue;
                    if (nested.ContainsKey(m.Key))
                    {
                        if (!((m.Value.Value.Count == 0) && (nested[m.Key].Value.Count == 0)))
                            throw new CompilerException(this, string.Format(Resource.Culture, Resource.ConflictingConstructor, b.typeReference.TypeName.Data));
                    }
                    else
                        nested.Add(m.Key, m.Value);
                }
            }

            // 4 walk the noparam constructors of the sparse extends insofar they have not allready been added
            foreach (DefinitionTypeReference t in ParentDefinition.SparseExtends)
            {
                if (result.ContainsKey(t) || nested.ContainsKey(t))
                    continue;
                Constructor c = t.Definition.FindConstructor(this, new FunctionTypeReference(this, t, new List<TypeReference>()), ParentDefinition);
                nested.Add(t, new KeyValuePair<Definition, List<Expression>>(ParentDefinition, new List<Expression>()));
                foreach (KeyValuePair<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> m in c.InnerResolveConstructor())
                {
                    if (result.ContainsKey(m.Key))
                        continue;
                    if (nested.ContainsKey(m.Key))
                    {
                        if (!((m.Value.Value.Count == 0) && (nested[m.Key].Value.Count == 0)))
                            throw new CompilerException(this, string.Format(Resource.Culture, Resource.ConflictingConstructor, t.TypeName.Data, m.Key.TypeName.Data));
                    }
                    else
                        nested.Add(m.Key, m.Value);
                }
            }

            foreach (KeyValuePair<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> m in nested)
                result.Add(m.Key, m.Value);

            return result;
        }

        private ConstructorNode InnerPrepareConstructors(Generator generator, Dictionary<DefinitionTypeReference, KeyValuePair<Definition, List<Expression>>> irc, Definition parentDefinition)
        {
            ConstructorNode result = new ConstructorNode();
            result.parentDefinition = parentDefinition;
            result.location = this;
            result.definition = ParentDefinition;
            result.runtimeStruct = generator.AllocateDataRegion();
            result.parameters = parametersMetadata;
            result.statement = statementMetadata;

            if (anotherConstructorInstance != null)
            {
                result.redirect = new ConstructorNodeInvocation(anotherConstructor, anotherConstructorInstance.InnerPrepareConstructors(generator, irc, parentDefinition));
            }
            else
            {
                result.inherit = new List<ConstructorNodeInvocation>();
                foreach (DefinitionTypeReference e in ParentDefinition.Extends)
                {
                    if (!irc.ContainsKey(e))
                        continue;
                    KeyValuePair<Definition, List<Expression>> kv = irc[e];
                    if (kv.Key == ParentDefinition)
                    {
                        List<TypeReference> signature = new List<TypeReference>();
                        foreach (Expression a in kv.Value)
                            signature.Add(null);
                        //                            signature.Add(a.TypeReference);
                        Constructor c = e.Definition.FindConstructor(this, new FunctionTypeReference(this, e, signature), generator.Resolver.CurrentDefinition);
                        ConstructorNodeInvocation cni = new ConstructorNodeInvocation(kv.Value, c.InnerPrepareConstructors(generator, irc, parentDefinition));
                        result.inherit.Add(cni);
                    }
                }
            }
            return result;
        }

        public override void Generate(Generator generator)
        {
            rootConstructorNode.GenerateFunction(generator);
            generator.Resolver.EnterContext();
            generator.Resolver.CurrentFieldName = ConstructorName();
            generator.AllocateAssembler();
            parametersMetadata.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.StartFunction();
            generator.Assembler.CallAllocator(generator.Allocator, ParentDefinition.InstanceSize, ParentDefinition.RuntimeStruct);
            int thisslot = generator.Resolver.ResolveSlotOffset(new Identifier(this, "this"));
            generator.Resolver.IncompleteSlot(thisslot, false);
            generator.Assembler.StoreVariable(thisslot);

            generator.Assembler.SetTypePart(rootConstructorNode.RuntimeStruct);
            generator.Assembler.PushValue();
            foreach (ParameterMetadata p in parametersMetadata.ParameterList)
            {
                generator.Assembler.RetrieveVariable(p.Slot);
                generator.Assembler.PushValue();
            }
            Placeholder retSite = generator.Assembler.CallFromStack(parametersMetadata.ParameterList.Count);
            generator.AddCallTraceEntry(retSite, this, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);

            generator.Resolver.LeaveContext();
            generator.Assembler.RetrieveVariable(thisslot);

            generator.Assembler.StopFunction();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
            generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "constructor:" + ParentDefinition.TypeReference.TypeName.Data + ".constructor");
            functionPointer = generator.Assembler.Region.BaseLocation;

            castFunction = new CallableCastFunction(this);
            castFunction.Generate(generator);
            if (ParentDefinition.GarbageCollectable)
                runtimeStruct.WriteNumber(1);
            else
                runtimeStruct.WriteNumber(0);
            runtimeStruct.WritePlaceholder(castFunction.FunctionPointer);
            runtimeStruct.WriteNumber(0);
            runtimeStruct.WriteNumber(0);
            runtimeStruct.WritePlaceholder(functionPointer);
            runtimeStruct.WritePlaceholder(ParentDefinition.RuntimeStruct);
            generator.Symbols.WriteData(runtimeStruct.BaseLocation, runtimeStruct.Length, "ms:" + ParentDefinition.TypeReference.TypeName.Data + ".constructor");
        }

        public override TypeReference AsTypeReference()
        {
            return new FunctionTypeReference(this, this);
        }

        private class BaseConstructor
        {
            public TypeName type;
            public DefinitionTypeReference typeReference;
            public List<Expression> arguments;

            public void Resolve(Generator generator)
            {
                foreach (Expression a in arguments)
                    a.Resolve(generator);
            }
        }

        private class ConstructorNode
        {
            public Definition parentDefinition;
            public ILocation location;
            public Definition definition;
            public Parameters parameters;
            public Statement statement;

            public ConstructorNodeInvocation redirect;
            public List<ConstructorNodeInvocation> inherit;
            public List<Field> assignedFields = new List<Field>();

            private Placeholder functionPointer;
            public Region runtimeStruct;

            public void Prepare(Generator generator)
            {
                runtimeStruct = generator.AllocateDataRegion();
            }

            public void MarkFieldsAssigned(Generator generator)
            {
                foreach (Field field in assignedFields)
                    generator.Resolver.AssignField(field);
            }

            public void MakeRuntimeStruct(Generator generator, Dictionary<Definition, Placeholder> implicitConversions)
            {
                CallableCastFunction castFunction = new CallableCastFunction(location);
                castFunction.Generate(generator);
                if (definition.GarbageCollectable)
                    runtimeStruct.WriteNumber(1);
                else
                    runtimeStruct.WriteNumber(0);
                runtimeStruct.WritePlaceholder(castFunction.FunctionPointer);
                runtimeStruct.WriteNumber(0);
                runtimeStruct.WriteNumber(0);
                runtimeStruct.WritePlaceholder(functionPointer);
                runtimeStruct.WritePlaceholder(implicitConversions[definition]);

                StringBuilder sb = new StringBuilder(256);
                sb.Append("cs:");
                sb.Append(definition.Name.Data);
                sb.Append(":");
                sb.Append(parentDefinition.Name.Data);
                sb.Append(".constructor");
                generator.Symbols.WriteData(runtimeStruct.BaseLocation, runtimeStruct.Length, sb.ToString());

                if (redirect != null)
                    redirect.node.MakeRuntimeStruct(generator, implicitConversions);
                else
                    foreach (ConstructorNodeInvocation l in inherit)
                        l.node.MakeRuntimeStruct(generator, implicitConversions);
            }

            private bool generated;

            public void GenerateFunction(Generator generator)
            {
                Require.False(generated);
                generated = true;
                if (redirect != null)
                    redirect.node.GenerateFunction(generator);
                else
                    foreach (ConstructorNodeInvocation i in inherit)
                        i.node.GenerateFunction(generator);

                generator.Resolver.EnterContext();
                generator.Resolver.EnterFakeContext(definition);

                StringBuilder sb = new StringBuilder();
                //sb.Append(parentDefinition.TypeReference.TypeName.Data);
                sb.Append("this");
                //sb.Append(definition.TypeReference.TypeName.Data);
                parameters.PrettyPrint(sb);
                string name = sb.ToString();
                generator.Resolver.CurrentFieldName = name;

                generator.AllocateAssembler();
                parameters.Generate(generator);

                ParameterMetadata thisParam = parameters.Find("this");
                generator.Resolver.SetImplicitFields(thisParam.Slot, thisParam.TypeReference);

                int thisslot = generator.Resolver.ResolveSlotOffset(new Identifier(location, "this"));
                generator.Resolver.IncompleteSlot(thisslot, false);
                
                statement.Prepare(generator);
                if (redirect == null)
                    definition.PrepareInitializer(generator);

                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, location);
                generator.Assembler.StartFunction();
                if (redirect != null)
                {
                    redirect.Call(generator);
                }
                else
                {
                    foreach (ConstructorNodeInvocation i in inherit)
                        i.Call(generator);
                    definition.GenerateInitializer(generator);
                }
                statement.Generate(generator, null);

                int slot = generator.Resolver.ResolveSlotOffset(new Identifier(location, "this"));
                generator.Resolver.RetrieveSlot(location, slot, false);

                foreach (Field field in definition.Fields)
                    if (generator.Resolver.IsFieldAssigned(field))
                        assignedFields.Add(field);

                generator.Resolver.LeaveFakeContext();
                generator.Resolver.LeaveContext();
                generator.Assembler.StopFunction();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, location, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "constructor:" + parentDefinition.TypeReference.TypeName.Data + "." + name);
                functionPointer = generator.Assembler.Region.BaseLocation;
            }

            public Placeholder RuntimeStruct { get { Require.Assigned(runtimeStruct); return runtimeStruct.BaseLocation; } }
        }

        private class ConstructorNodeInvocation
        {
            public ConstructorNode node;
            private List<Expression> arguments;

            public ConstructorNodeInvocation(List<Expression> arguments, ConstructorNode node)
            {
                this.node = node;
                this.arguments = arguments;
                Require.True(arguments.Count == node.parameters.ParameterList.Count);
            }

            public void Call(Generator generator)
            {
                ILocation location = new NowhereLocation();
                SlotExpression e = new SlotExpression(location, new Identifier(location, "this"), true);
                e.Resolve(generator);
                e.Prepare(generator, null);
                e.Generate(generator);
                generator.Assembler.SetTypePart(node.RuntimeStruct);
                generator.Assembler.PushValue();
                int i = 0;
                foreach (Expression a in arguments)
                {
                    a.Prepare(generator, node.parameters.ParameterList[i].TypeReference);
                    a.Generate(generator);
                    node.parameters.ParameterList[i].TypeReference.GenerateConversion(a, generator, a.TypeReference);
                    generator.Assembler.PushValue();
                    i++;
                }
                Placeholder retSite = generator.Assembler.CallFromStack(arguments.Count);
                generator.AddCallTraceEntry(retSite, location, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);
                node.MarkFieldsAssigned(generator);
            }
        }
    }
}

