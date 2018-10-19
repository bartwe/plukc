using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Definition : NodeBase
    {
        private const int fixedFields = 4;
        private Modifiers modifiers;
        private TypeName name;
        private DefinitionTypeReference typeReference;
        private List<Field> localFields = new List<Field>();
        private List<Field> fields;
        private List<Field> staticFields;
        private List<Constructor> constructors = new List<Constructor>();
        private List<Method> localMethods = new List<Method>();
        private List<Method> localTemplateMethods = new List<Method>();
        private List<Method> methods;
        private List<Property> localProperties = new List<Property>();
        private List<Property> properties;
        private Dictionary<string, Method> methodsMap = new Dictionary<string, Method>();
        private Dictionary<string, Property> propertiesMap = new Dictionary<string, Property>();
        private Region runtimeStructure;
        private List<TypeName> extendsTypeNames = new List<TypeName>();
        private List<DefinitionTypeReference> extends = new List<DefinitionTypeReference>();
        private List<DefinitionTypeReference> sparseExtends;
        private Set<string> imports;
        private List<Identifier> templateParamaters = new List<Identifier>();
        private DefinitionCastFunction castFunction;
        private Region classDescription;
        private List<Statement> staticInitializers = new List<Statement>();
        private Placeholder staticInitializerFunctionPointer;
        private Set<TypeReference> dependsUpon = new Set<TypeReference>();
        private bool fieldsInitialized;
        private bool staticFieldsInitialized;
        private List<Statement> initializers = new List<Statement>();
        private List<Method> templateMethods = new List<Method>();

        public int InstanceSize { get { return fields.Count; } }
        public List<Field> Fields { get { return fields; } }
        public List<Field> LocalFields { get { return localFields; } }
        public List<Method> LocalMethods { get { return localMethods; } }
        public List<Method> LocalTemplateMethods { get { return localTemplateMethods; } }
        public List<Constructor> Constructors { get { return constructors; } }
        public bool IsTemplate { get { return templateParamaters.Count > 0; } }
        public DefinitionTypeReference TypeReference { get { return typeReference; } }
        public List<DefinitionTypeReference> Extends { get { return extends; } }
        public List<DefinitionTypeReference> SparseExtends { get { Require.Assigned(sparseExtends); return sparseExtends; } }
        public Modifiers Modifiers { get { return modifiers; } }
        public Placeholder StaticInitializerFunctionPointer { get { return staticInitializerFunctionPointer; } }
        public int DependsOrder;
        public bool FieldsInitialized { get { return fieldsInitialized; } }
        public bool StaticFieldsInitialized { get { return staticFieldsInitialized; } }

        bool? garbageCollectable;
        public bool GarbageCollectable  { get { return garbageCollectable.Value; } }


        public Placeholder RuntimeStruct
        {
            get { return runtimeStructure.BaseLocation; }
        }

        public Definition(ILocation location, Set<string> imports, Modifiers modifiers)
            : base(location)
        {
            if (imports == null)
                throw new ArgumentNullException("imports");
            if (modifiers == null)
                throw new ArgumentNullException("modifiers");
            this.imports = imports;
            this.modifiers = modifiers;
            typeReference = new DefinitionTypeReference(this, this);
        }

        public TypeName Name { get { return name; } }

        public Definition InstantiateTemplate(TypeName typeName, List<TypeReference> parameters)
        {
            if (parameters.Count != templateParamaters.Count)
            {
                TypeName myTypeName = new TypeName(name.PrimaryName);
                foreach (Identifier param in templateParamaters)
                    myTypeName.AddTemplateParameter(new TypeName(param));
                throw new CompilerException(typeName, string.Format(Resource.Culture, Resource.TemplateParameterMismatch, typeName.Data, myTypeName.Data));
            }
            Dictionary<string, TypeName> parameterMapping = new Dictionary<string, TypeName>();
            for (int i = 0; i < parameters.Count; ++i)
                parameterMapping[templateParamaters[i].Data] = parameters[i].TypeName;
            Definition result = new Definition(this, imports, modifiers);
            TypeName definitionName = new TypeName(typeName.PrimaryName, Compiler.Metadata.Nullability.NotNullable);
            foreach (TypeReference tr in parameters)
                definitionName.AddTemplateParameter(tr.TypeName);
            result.name = definitionName;
            result.UpdateNameMetadata();

            foreach (Field field in localFields)
            {
                Field fieldMetadata = field.InstantiateTemplate(parameterMapping);
                fieldMetadata.SetParentDefinition(result);
                result.localFields.Add(fieldMetadata);
            }
            foreach (Method method in localMethods)
            {
                Method methodMetadata = method.InstantiateTemplate(parameterMapping);
                methodMetadata.SetParentDefinition(result);
                result.localMethods.Add(methodMetadata);
            }
            foreach (Property property in localProperties)
            {
                Property propertyMetadata = property.InstantiateTemplate(parameterMapping);
                propertyMetadata.SetParentDefinition(result);
                result.localProperties.Add(propertyMetadata);
            }
            foreach (Method method in localTemplateMethods)
            {
                Method methodMetadata = method.InstantiateTemplate(parameterMapping);
                methodMetadata.SetParentDefinition(result);
                result.localTemplateMethods.Add(methodMetadata);
            }
            foreach (Constructor constructor in constructors)
            {
                Constructor c = constructor.InstantiateTemplate(parameterMapping);
                c.SetParentDefinition(result);
                result.Constructors.Add(c);
            }
            foreach (TypeName extends in extendsTypeNames)
                result.extendsTypeNames.Add(extends.InstantiateTemplate(parameterMapping));
            foreach (Statement statement in staticInitializers)
                result.staticInitializers.Add(statement.InstantiateTemplate(parameterMapping));
            foreach (Statement statement in initializers)
                result.initializers.Add(statement.InstantiateTemplate(parameterMapping));

            return result;
        }

        public bool Supports(DefinitionTypeReference type)
        {
            if (type == typeReference)
                return true;
            if (extends.Contains(type))
                return true;
            return false;
        }

        public int SupportsDistance(DefinitionTypeReference type)
        {
            if (type == typeReference)
                return 0;
            if (sparseExtends.Contains(type))
                return 1;
            int lowest = int.MaxValue - 1;
            foreach (DefinitionTypeReference extends in sparseExtends)
            {
                int c = extends.Definition.SupportsDistance(type);
                if (c < lowest)
                    lowest = c;
            }
            return lowest + 1;
        }

        public void DependsUpon(TypeReference type)
        {
            Require.Assigned(type);
            dependsUpon.Put(type);
        }

        private bool updatingDepends;

        private void SetDependsOrder(int order)
        {
            if (updatingDepends)
                return;
            if (order > DependsOrder)
            {
                DependsOrder = order;
                InnerUpdateDependsOrder();
            }
        }

        public void UpdateDependsOrder()
        {
            if (DependsOrder == 0)
            {
                DependsOrder = 1;
                InnerUpdateDependsOrder();
            }
        }

        public void InnerUpdateDependsOrder()
        {
            int d = DependsOrder + 1;
            updatingDepends = true;
            foreach (TypeReference t in dependsUpon)
                if (t.IsDefinition)
                {
                    Definition def = ((DefinitionTypeReference)t).Definition;
                    def.SetDependsOrder(d);
                }
            updatingDepends = false;
        }

        public IEnumerable<KeyValuePair<int, DefinitionTypeReference>> GetSupportedTypesMap()
        {
            List<KeyValuePair<int, DefinitionTypeReference>> result = new List<KeyValuePair<int, DefinitionTypeReference>>();
            result.Add(new KeyValuePair<int, DefinitionTypeReference>(-1, typeReference));
            foreach (DefinitionTypeReference ex in extends)
                result.Add(new KeyValuePair<int, DefinitionTypeReference>(GetConversionOffset(ex), ex));
            return result;
        }

        public void SetName(Identifier name)
        {
            if (this.name != null)
                throw new InvalidOperationException();
            if (name == null)
                throw new ArgumentNullException("name");
            this.name = new TypeName(name, Nullability.NotNullable);
            this.name.SetHasNamespace();
            if (name.Data != "pluk.base.Object")
                AddExtends(new TypeName(new Identifier(this, "pluk.base.Object")));
            UpdateNameMetadata();
        }

        void UpdateNameMetadata() {
            garbageCollectable =
                    (name.DataModifierLess == "pluk.base.Bool") ||
                    (name.DataModifierLess == "pluk.base.Byte") ||
                    (name.DataModifierLess == "pluk.base.Int") ||
                    (name.DataModifierLess == "pluk.base.Float") ||
                    (name.DataModifierLess == "pluk.base.Type") ||
                    (name.DataModifierLess == "pluk.base.StaticString");
        }

        public void AddStaticInitializer(Statement statement)
        {
            Require.Assigned(statement);
            staticInitializers.Add(statement);
        }

        public void AddInitializer(Statement statement)
        {
            Require.Assigned(statement);
            initializers.Add(statement);
        }

        public void AddTemplateParameter(Identifier parameter)
        {
            templateParamaters.Add(parameter);
        }

        public void AddExtends(TypeName type)
        {
            extendsTypeNames.Add(new TypeName(type, Nullability.NotNullable));
        }

        public void GetExtends(Set<DefinitionTypeReference> set)
        {
            set.PutRange(extends);
        }

        private bool SupportsType(DefinitionTypeReference type)
        {
            return extends.Contains(type);
        }

        public void AddConstructor(Constructor constructorMetadata)
        {
            if (constructorMetadata == null)
                throw new ArgumentNullException("constructorMetadata");
            constructors.Add(constructorMetadata);
            constructorMetadata.SetParentDefinition(this);
        }

        public void AddField(Field fieldMetadata)
        {
            CheckFieldName(fieldMetadata.Name, false);
            localFields.Add(fieldMetadata);
            fieldMetadata.SetParentDefinition(this);
        }

        public void AddMethod(Method methodMetadata)
        {
            CheckFieldName(methodMetadata.Name, true);
            if (methodMetadata.IsTemplateMethod)
                localTemplateMethods.Add(methodMetadata);
            else
                localMethods.Add(methodMetadata);
            methodMetadata.SetParentDefinition(this);
        }

        public void AddProperty(Property propertyMetadata)
        {
            CheckFieldName(propertyMetadata.Name, false);
            localProperties.Add(propertyMetadata);
            propertyMetadata.SetParentDefinition(this);
        }

        private void CheckFieldName(Identifier name, bool skipMethods)
        {
            foreach (Field field in localFields)
                if (field.Name.Data == name.Data)
                    throw new CompilerException(name, string.Format(Resource.Culture, Resource.FieldWithNameAlreadyDefiniedOnClass, name.Data));
            if (!skipMethods)
            {
                foreach (Method method in localMethods)
                    if (method.Name.Data == name.Data)
                        throw new CompilerException(name, string.Format(Resource.Culture, Resource.FieldWithNameAlreadyDefiniedOnClass, name.Data));
                foreach (Method method in localTemplateMethods)
                    if (method.Name.Data == name.Data)
                        throw new CompilerException(name, string.Format(Resource.Culture, Resource.FieldWithNameAlreadyDefiniedOnClass, name.Data));
            }
            foreach (Property property in localProperties)
                if (property.Name.Data == name.Data)
                    throw new CompilerException(name, string.Format(Resource.Culture, Resource.FieldWithNameAlreadyDefiniedOnClass, name.Data));
        }

        private void CheckIfMethodsAreUnique()
        {
            foreach (Method method in localMethods)
                foreach (Method inner in localMethods)
                    if ((inner != method) && (inner.Name.Data == method.Name.Data))
                    {
                        if (method.Parameters.Same(inner.Parameters))
                            throw new CompilerException(method.Name, string.Format(Resource.Culture, Resource.FieldWithNameAlreadyDefiniedOnClass, method.Signature()));
                    }
        }

        public void Complete()
        {
            if (name == null)
                throw new InvalidOperationException();
        }

        private bool resolved;

        public void Resolve(Generator generator)
        {
            Require.False(resolved);
            resolved = true;
            generator.Resolver.SetImportsContext(imports);
            Require.Equals(typeReference, generator.Resolver.ResolveDefinitionType(name, name));
            foreach (TypeName typeName in extendsTypeNames)
                extends.Add(generator.Resolver.ResolveDefinitionType(typeName, typeName));
            if (constructors.Count == 0)
            {
                Constructor c = new Constructor(this, new Modifiers(this), new Parameters());
                AddConstructor(c);
                c.SetBody(new EmptyStatement(this));
            }
            foreach (Field field in localFields)
                field.Resolve(generator);
            foreach (Property property in localProperties)
                property.Resolve(generator);
            foreach (Method method in localMethods)
                method.Resolve(generator);
            CheckIfMethodsAreUnique();
            foreach (Constructor constructor in constructors)
                constructor.Resolve(generator);
            foreach (Statement statement in staticInitializers)
                statement.Resolve(generator);
            foreach (Statement statement in initializers)
                statement.Resolve(generator);
        }

        // collects all extends, and the extends their extends
        // sort it so that dependencies are earlier in the list
        public void PrepareExtends()
        {
            Set<DefinitionTypeReference> toVisit = new Set<DefinitionTypeReference>();
            Set<DefinitionTypeReference> newVisitables = new Set<DefinitionTypeReference>();
            newVisitables.AddRange(extends);
            Set<DefinitionTypeReference> loop = new Set<DefinitionTypeReference>();
            while (newVisitables.Count > 0)
            {
                foreach (DefinitionTypeReference typeReference in newVisitables)
                    if (!toVisit.Contains(typeReference))
                        loop.Add(typeReference);
                toVisit.AddRange(loop);
                newVisitables.Clear();
                foreach (DefinitionTypeReference typeReference in loop)
                    typeReference.Definition.GetExtends(newVisitables);
                loop.Clear();
            }

            List<DefinitionTypeReference> preSortables = new List<DefinitionTypeReference>(toVisit);
            preSortables.Sort(SortExtends);
            Queue<DefinitionTypeReference> sortables = new Queue<DefinitionTypeReference>(preSortables);
            extends.Clear();
            while (sortables.Count > 0)
            {
                DefinitionTypeReference item = sortables.Dequeue();
                foreach (DefinitionTypeReference type in sortables)
                    if (item.Definition.SupportsType(type))
                    {
                        // woops failue, try again later
                        sortables.Enqueue(item);
                        item = null;
                        break;
                    }
                if (item != null)
                    extends.Add(item);
            }
            sparseExtends = new List<DefinitionTypeReference>();
            foreach (DefinitionTypeReference e in extends)
            {
                bool hit = true;
                foreach (DefinitionTypeReference x in extends)
                {
                    if (x != e)
                        if (e.Supports(x))
                        {
                            hit = false;
                            break;
                        }
                }
                if (hit)
                    sparseExtends.Add(e);
            }
        }

        private int SortExtends(DefinitionTypeReference l, DefinitionTypeReference r)
        {
            return l.Definition.Name.Data.CompareTo(r.Definition.Name.Data);
        }

        private void AddProperties(List<Property> properties)
        {
            foreach (Property property in properties)
            {
                string name = property.Name.Data;
                if (propertiesMap.ContainsKey(name))
                {
                    if (((!property.GetModifiers.Override) || (!property.SetModifiers.Override)) && (Name.Data == property.ParentDefinition.Name.Data))
                        throw new CompilerException(property, string.Format(Resource.MissingPropertyOverride, property.Name.Data, Name.Data));
                    this.properties.Remove(propertiesMap[name]);
                    propertiesMap.Remove(name);
                }
                else
                    if ((property.GetModifiers.Override) || (property.SetModifiers.Override))
                        throw new CompilerException(property, string.Format(Resource.MissingPropertyOverrideTarget, property.Name.Data, Name.Data));

                propertiesMap.Add(name, property);
                this.properties.Add(property);
            }
        }

        private void AddMethods(List<Method> methods)
        {
            foreach (Method method in methods)
            {
                string name = method.Signature();
                if (methodsMap.ContainsKey(name))
                {
                    Method baseMethod = methodsMap[name];
                    if ((!method.Modifiers.Override) && (Name.Data == method.ParentDefinition.Name.Data))
                        throw new CompilerException(method, string.Format(Resource.MissingMethodOverride, method.Name.Data, Name.Data));
                    if (!baseMethod.ReturnType.Equals(method.ReturnType))
                        throw new CompilerException(method, string.Format(Resource.MethodOverrideReturnTypeMismatch, method.Name.Data, Name.Data));
                    this.methods.Remove(baseMethod);
                    methodsMap.Remove(name);
                }
                else
                    if (method.Modifiers.Override)
                        throw new CompilerException(method, string.Format(Resource.MissingMethodOverrideTarget, method.Name.Data, Name.Data));

                methodsMap.Add(name, method);
                this.methods.Add(method);
            }
        }

        private void AddTemplateMethods(List<Method> methods)
        {
            foreach (Method method in methods)
            {
                templateMethods.Add(method);
            }
        }

        public void BeforePrepare()
        {
            Require.Unassigned(fields);
            Require.Unassigned(staticFields);
            fields = new List<Field>();
            staticFields = new List<Field>();
            foreach (Field field in localFields)
            {
                if (!field.GetModifiers.Static)
                    fields.Add(field);
                else
                    staticFields.Add(field);
            }
            properties = new List<Property>();
            methods = new List<Method>();
            foreach (DefinitionTypeReference extend in extends)
            {
                foreach (Field field in extend.Definition.LocalFields)
                    if (!field.GetModifiers.Static)
                        fields.Add(field);
                    else
                        staticFields.Add(field);
                AddProperties(extend.Definition.localProperties);
                AddMethods(extend.Definition.LocalMethods);
                AddTemplateMethods(extend.Definition.LocalTemplateMethods);
            }
            AddProperties(localProperties);
            AddMethods(localMethods);
            AddTemplateMethods(localTemplateMethods);

            templateMethods.Reverse(); // prefer early entries

            if (!Modifiers.Abstract)
            {
                foreach (Method method in methods)
                    if (method.Modifiers.Abstract)
                        throw new CompilerException(this, string.Format(Resource.MethodIsAbstract, method.Name.Data, Name.Data));
                foreach (Property property in properties)
                    if (property.GetModifiers.Abstract || property.SetModifiers.Abstract)
                        throw new CompilerException(this, string.Format(Resource.PropertyIsAbstract, property.Name.Data, Name.Data));
            }
        }

        public void Prepare(Generator generator)
        {
            foreach (TypeReference e in extends)
                dependsUpon.Put(e);
            generator.Resolver.EnterDefinitionContext(this);
            foreach (Field field in localFields)
                field.Prepare(generator, dependsUpon);
            foreach (Field field in staticFields)
                field.PrepareStatic(generator, dependsUpon);
            foreach (Constructor constructor in constructors)
                constructor.Prepare(generator, dependsUpon);
            foreach (Method method in localMethods)
                method.Prepare(generator, dependsUpon);
            foreach (Property property in localProperties)
                property.Prepare(generator, dependsUpon);
            runtimeStructure = generator.AllocateDataRegion();
            classDescription = generator.AllocateDataRegion();
            WriteTypeDescription(classDescription, generator);
            generator.Resolver.LeaveContext();
        }

        private void WriteTypeDescription(Region description, Generator generator)
        {
            description.WriteNumber(1);
            description.WritePlaceholder(generator.AddTextLengthPrefix(name.DataModifierLess));
        }

        public void PrepareNestedConstructors(Generator generator)
        {
            generator.Resolver.EnterDefinitionContext(this);
            foreach (Constructor constructor in constructors)
                constructor.PrepareNestedConstructors(generator);
            generator.Resolver.LeaveContext();
        }

        public void Generate(Generator generator)
        {
            generator.Resolver.EnterDefinitionContext(this);
            GenerateStaticInitializer(generator);
            staticFieldsInitialized = true;
            foreach (Constructor constructor in constructors)
                constructor.Generate(generator);
            fieldsInitialized = true;
            generator.Resolver.CheckContextEmpty();
            foreach (Method method in localMethods)
            {
                method.Generate(generator);
                generator.Resolver.CheckContextEmpty();
            }
            foreach (Property property in localProperties)
                property.Generate(generator);
            generator.Resolver.LeaveContext();
            castFunction = new DefinitionCastFunction(this);
            castFunction.Generate(generator);
            GenerateRuntimeStructure(generator);
        }

        private void GenerateStaticInitializer(Generator generator)
        {
            if (staticInitializers.Count == 0)
                return;
            generator.AllocateAssembler();
            generator.Resolver.EnterContext();
            int staticThisSlot = generator.Assembler.AddVariable();
            StaticTypeReference staticThisType = new StaticTypeReference(this, TypeReference);
            generator.Resolver.AddVariable(new Identifier(this, "this"), staticThisType, staticThisSlot, true);
            foreach (Statement statement in staticInitializers)
            {
                generator.Resolver.EnterContext();
                statement.Prepare(generator);
                generator.Resolver.LeaveAndMergeContext();
            }
            generator.Assembler.StartFunction();
            generator.Resolver.SetImplicitFields(staticThisSlot, staticThisType);
            TypeExpression te = new TypeExpression(this, staticThisType);
            te.Resolve(generator);
            te.Prepare(generator, null);
            te.Generate(generator);
            generator.Assembler.StoreVariable(staticThisSlot);
            foreach (Statement statement in staticInitializers)
            {
                generator.Resolver.EnterContext();
                statement.Generate(generator, null);
                generator.Resolver.LeaveAndMergeContext();
            }
            CheckAllStaticFieldsAssigned(this, generator.Resolver, true);
            generator.Assembler.StopFunction();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
            generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "staticInitializer:" + Name.Data);
            staticInitializerFunctionPointer = generator.Assembler.Region.BaseLocation;
            generator.Resolver.LeaveContext();
        }

        public void PrepareInitializer(Generator generator)
        {
            if (initializers.Count == 0)
                return;
            generator.Resolver.EnterContext();
            foreach (Statement statement in initializers)
            {
                generator.Resolver.EnterContext();
                statement.Prepare(generator);
                generator.Resolver.LeaveAndMergeContext();
            }
            generator.Resolver.LeaveAndMergeContext();
        }

        public void GenerateInitializer(Generator generator)
        {
            if (initializers.Count == 0)
                return;
            generator.Resolver.EnterContext();
            foreach (Statement statement in initializers)
            {
                generator.Resolver.EnterContext();
                statement.Generate(generator, null);
                generator.Resolver.LeaveAndMergeContext();
            }
            generator.Resolver.LeaveAndMergeContext();
        }

        private void GenerateRuntimeStructure(Generator generator)
        {
            Dictionary<Definition, Region> types = new Dictionary<Definition, Region>();
            Dictionary<Definition, Placeholder> implicitTypeConversions = new Dictionary<Definition, Placeholder>();
            types[this] = runtimeStructure;
            implicitTypeConversions[this] = runtimeStructure.BaseLocation;
            foreach (DefinitionTypeReference tr in extends)
            {
                Region rts = generator.AllocateDataRegion();
                types.Add(tr.Definition, rts);
                implicitTypeConversions.Add(tr.Definition, rts.BaseLocation);
            }
            Dictionary<string, int> fieldOffsets = new Dictionary<string, int>();
            int fieldIndex = 0;
            foreach (Field field in fields)
            {
                if (fieldOffsets.ContainsKey(field.LocalName))
                    throw new CompilerException(this, string.Format(Resource.FieldNameCollision, field.Name.Data));
                fieldOffsets.Add(field.LocalName, fieldIndex);
                fieldIndex++;
            }
            Dictionary<string, Method> methodOverrides = new Dictionary<string, Method>();
            foreach (Method method in methods)
                methodOverrides.Add(method.Signature(), method);
            Dictionary<string, Property> propertyOverrides = new Dictionary<string, Property>();
            foreach (Property property in properties)
                propertyOverrides.Add(property.Name.Data, property);
            foreach (Definition definition in types.Keys)
                definition.GenerateRuntimeStructure(generator, this, types[definition], implicitTypeConversions, fieldOffsets, methodOverrides, propertyOverrides);

            foreach (DefinitionTypeReference tr in extends)
            {
                Region rts = types[tr.Definition];
                generator.Symbols.WriteData(rts.BaseLocation, rts.Length, "class:" + name.Data + ":" + tr.Definition.Name.Data);
            }
        }

        private void GenerateRuntimeStructure(Generator generator, Definition baseDefinition, Region runtimeStructure, Dictionary<Definition, Placeholder> implicitTypeConversions, Dictionary<string, int> slotOffsets, Dictionary<string, Method> methodOverrides, Dictionary<string, Property> propertyOverrides)
        {
            Placeholder rsl = runtimeStructure.CurrentLocation;
            if (baseDefinition.GarbageCollectable)
                runtimeStructure.WriteNumber(1);
            else
                runtimeStructure.WriteNumber(0);
            runtimeStructure.WritePlaceholder(baseDefinition.castFunction.FunctionPointer);
            runtimeStructure.WritePlaceholder(baseDefinition.classDescription.BaseLocation);
            runtimeStructure.WriteNumber(0);
            foreach (Constructor constructor in constructors)
                runtimeStructure.WritePlaceholder(constructor.MakeRuntimeStruct(generator, implicitTypeConversions));
            foreach (Method localMethod in methods)
            {
                Method method = methodOverrides[localMethod.Signature()];
                runtimeStructure.WritePlaceholder(method.MakeMethodStruct(generator, baseDefinition, implicitTypeConversions[method.ParentDefinition]));
            }
            foreach (Property localProperty in properties)
            {
                Property property = propertyOverrides[localProperty.Name.Data];
                runtimeStructure.WritePlaceholder(property.MakeGetPropertyStruct(generator, baseDefinition, implicitTypeConversions[property.ParentDefinition]));
                runtimeStructure.WritePlaceholder(property.MakeSetPropertyStruct(generator, baseDefinition, implicitTypeConversions[property.ParentDefinition]));
            }
            foreach (Field field in fields)
                runtimeStructure.WriteNumberTimesBitness(slotOffsets[field.LocalName] * 2);
            foreach (DefinitionTypeReference tr in extends)
                runtimeStructure.WritePlaceholder(implicitTypeConversions[tr.Definition]);
            generator.Symbols.WriteData(rsl, runtimeStructure.CurrentLocation.MemoryDistanceFrom(rsl), "class:" + name.Data);
        }

        public Method FindMethod(Identifier name, bool staticRef, TypeReference inferredType, Definition context, bool throws)
        {
            List<Method> results = new List<Method>();
            foreach (Method method in methods)
            {
                if (!staticRef || method.Modifiers.Static)
                    if (method.Name.Data == name.Data)
                        results.Add(method);
            }
            foreach (Method method in templateMethods)
            {
                if (!staticRef || method.Modifiers.Static)
                    if (method.Name.Data == name.Data)
                        results.Add(method);
            }
            if ((inferredType != null) && (inferredType.IsNullable))
                inferredType = ((NullableTypeReference)inferredType).Parent;
            FunctionTypeReference ftr = inferredType as FunctionTypeReference;
            if (ftr == null)
            {
                if (results.Count == 1)
                {
                    Method method = results[0];
                    if (context != null)
                        if (!method.Modifiers.CheckVisibility(method.ParentDefinition, context))
                        {
                            throw new Compiler.CompilerException(name, string.Format(Resource.NoVisibleMethod, name.Data));
                        }
                    if (method.IsTemplateMethod)
                        throw new Compiler.CompilerException(name, string.Format(Resource.AmbiguousMethodReference, name.Data));
                    return method;
                }
                if (!throws)
                    return null;
                throw new Compiler.CompilerException(name, string.Format(Resource.AmbiguousMethodReference, name.Data));
            }
            Stack<Method> filtered = new Stack<Method>();
            bool matching = false;
            foreach (Method method in results)
                if (method.Parameters.Match(ftr.FunctionParameters))
                {
                    matching = true;
                    if (context != null)
                        if (!method.Modifiers.CheckVisibility(method.ParentDefinition, context))
                            continue;
                    filtered.Push(method);
                }
            if (filtered.Count == 0)
            {
                if (!throws)
                    return null;
                if (matching)
                    throw new Compiler.CompilerException(name, string.Format(Resource.NoVisibleMethod, name.Data));
                if (results.Count == 0)
                    throw new Compiler.CompilerException(name, string.Format(Resource.NoMatchingMethod, name.Data));
                if (results.Count == 1)
                    throw new Compiler.CompilerException(name, string.Format(Resource.NoMatchingMethodOption, name.Data, results[0].AsTypeReference().TypeName.Data));
                else
                    throw new Compiler.CompilerException(name, string.Format(Resource.NoMatchingMethodMultiple, name.Data));
            }
            Method preferred = filtered.Pop();
            while (filtered.Count > 0)
            {
                Method m = filtered.Pop();
                int c = preferred.Parameters.CompareTo(m.Parameters, ftr.FunctionParameters);
                if (c == 0)
                {
                    if (!throws)
                        return null;
                    throw new Compiler.CompilerException(name, string.Format(Resource.AmbiguousMethodReference, name.Data));
                }
                if (c < 0)
                    preferred = m;
            }
            return preferred;
        }

        public Constructor FindConstructor(ILocation location, TypeReference inferredType, Definition context)
        {
            List<Constructor> results = new List<Constructor>(constructors);
            if ((inferredType != null) && inferredType.IsNullable)
                inferredType = ((NullableTypeReference)inferredType).Parent;
            FunctionTypeReference ftr = inferredType as FunctionTypeReference;
            Stack<Constructor> filtered = new Stack<Constructor>();
            bool matching = false;
            foreach (Constructor constructor in results)
            {
                if ((ftr == null) || constructor.Parameters.Match(ftr.FunctionParameters))
                {
                    matching = true;
                    if (constructor.Modifiers.CheckVisibility(constructor.ParentDefinition, context))
                        filtered.Push(constructor);
                }
            }
            if (filtered.Count == 0)
            {
                if (matching)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.NoVisibleConstructor, this.TypeReference.TypeName.Data));
                if (ftr != null)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.NoMatchingConstructorSignature, this.TypeReference.TypeName.Data, ftr.TypeName.Data));
                else
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.NoMatchingConstructor, this.TypeReference.TypeName.Data));
            }
            if ((ftr == null) && (filtered.Count != 1))
            {
                throw new CompilerException(location, string.Format(Resource.Culture, Resource.AmbiguousConstructorReference, this.TypeReference.TypeName.Data));
            }
            Constructor preferred = filtered.Pop();
            while (filtered.Count > 0)
            {
                Constructor m = filtered.Pop();
                int c = preferred.Parameters.CompareTo(m.Parameters, ftr.FunctionParameters);
                if (c == 0)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.AmbiguousConstructorReference, this.TypeReference.TypeName.Data));
                if (c < 0)
                    preferred = m;
            }
            return preferred;
        }

        public bool HasMethod(Identifier name, bool staticRef)
        {
            foreach (Method method in methods)
            {
                if (!staticRef || method.Modifiers.Static)
                    if (method.Name.Data == name.Data)
                        return true;
            }
            foreach (Method method in localTemplateMethods)
            {
                if (!staticRef || method.Modifiers.Static)
                    if (method.Name.Data == name.Data)
                        return true;
            }
            return false;
        }

        public bool HasMethod(Identifier name)
        {
            return HasMethod(name, false);
        }

        public int GetMethodOffset(ILocation location, Method method, Definition context)
        {
            int i = fixedFields + constructors.Count; // skip constructor and flags
            foreach (Method m in methods)
            {
                if (m == method)
                {
                    if (!method.Modifiers.CheckVisibility(method.ParentDefinition, context))
                        throw new CompilerException(location, string.Format(Resource.MethodNotVisible, method.Name.Data));
                    return i;
                }
                ++i;
            }
            Require.NotCalled();
            return -1;
        }

        public bool HasProperty(Identifier name, bool staticRef)
        {
            foreach (Property property in properties)
            {
                if (!staticRef || (property.GetModifiers.Static && property.SetModifiers.Static))
                    if (property.Name.Data == name.Data)
                        return true;
            }
            return false;
        }

        public bool HasProperty(Identifier name)
        {
            foreach (Property property in properties)
            {
                if (property.Name.Data == name.Data)
                    return true;
            }
            return false;
        }

        public int GetGetPropertyOffset(ILocation location, Identifier name, Definition context)
        {
            int i = fixedFields + constructors.Count + methods.Count; // skip flags and methods
            foreach (Property property in properties)
            {
                if (property.Name.Data == name.Data)
                {
                    if (!property.GetModifiers.CheckVisibility(property.ParentDefinition, context))
                        throw new CompilerException(location, string.Format(Resource.GetPropertyNotVisible, property.Name.Data));
                    return i;
                }
                i += 2;
            }
            Require.NotCalled();
            return -1;
        }

        public int GetSetPropertyOffset(ILocation location, Identifier name, Definition context)
        {
            int i = fixedFields + constructors.Count + methods.Count + 1; // skip constructor and flags and methods
            foreach (Property property in properties)
            {
                if (property.Name.Data == name.Data)
                {
                    if (!property.SetModifiers.CheckVisibility(property.ParentDefinition, context))
                        throw new CompilerException(location, string.Format(Resource.SetPropertyNotVisible, property.Name.Data));
                    return i;
                }
                i += 2;
            }
            Require.NotCalled();
            return -1;
        }

        public Property GetProperty(Identifier name)
        {
            foreach (Property property in properties)
                if (property.Name.Data == name.Data)
                    return property;
            Require.NotCalled();
            return null;
        }

        public bool CheckAllFieldsAssigned(ILocation location, Resolver resolver, bool completeConstrucor)
        {
            foreach (Field field in fields)
                if (!field.CheckAssigned(resolver))
                {
                    if (completeConstrucor)
                        throw new CompilerException(location, string.Format(Resource.Culture, Resource.ObjectNotFullyAssignedConstructor, name.Data, field.Name.Data));
                    return false;
                }
            return CheckAllStaticFieldsAssigned(location, resolver, completeConstrucor);
        }

        public bool CheckAllStaticFieldsAssigned(ILocation location, Resolver resolver, bool completeConstrucor)
        {
            if (staticFieldsInitialized)
                return true;
            foreach (Field field in staticFields)
                if (!field.CheckAssigned(resolver))
                {
                    if (completeConstrucor)
                        throw new CompilerException(location, string.Format(Resource.Culture, Resource.ObjectNotFullyAssignedConstructor, name.Data, field.Name.Data));
                    return false;
                }
            return true;
        }

        public bool HasField(Identifier name, bool staticRef)
        {
            if (!staticRef)
                foreach (Field field in fields)
                    if (field.Name.Data == name.Data)
                        return true;
            foreach (Field field in staticFields)
                if (field.Name.Data == name.Data)
                    return true;
            return false;
        }

        public bool HasField(Identifier name)
        {
            foreach (Field field in fields)
                if (field.Name.Data == name.Data)
                    return true;
            foreach (Field field in staticFields)
                if (field.Name.Data == name.Data)
                    return true;
            return false;
        }

        public int GetFieldOffset(ILocation location, Identifier name, Definition context, bool forWrite)
        {
            int i = fixedFields + constructors.Count + methods.Count + (2 * properties.Count); // skip constructor, flags and methods
            foreach (Field field in fields)
            {
                if (field.Name.Data == name.Data)
                {
                    if (forWrite)
                    {
                        if (!field.SetModifiers.CheckVisibility(field.ParentDefinition, context))
                            throw new CompilerException(location, string.Format(Resource.FieldNotVisible, field.Name.Data));
                    }
                    else
                    {
                        if (!field.GetModifiers.CheckVisibility(field.ParentDefinition, context))
                            throw new CompilerException(location, string.Format(Resource.FieldNotVisible, field.Name.Data));
                    }
                    return i;
                }
                ++i;
            }
            Require.NotCalled();
            return -1;
        }

        public Field GetField(Identifier name, bool staticRef)
        {
            if (!staticRef)
                foreach (Field field in fields)
                {
                    if (field.Name.Data == name.Data)
                        return field;
                }
            foreach (Field field in staticFields)
            {
                if (field.Name.Data == name.Data)
                    return field;
            }
            Require.NotCalled();
            return null;
        }

        public int GetConversionOffset(DefinitionTypeReference type)
        {
            Require.True(Supports(type));
            int i = fixedFields + constructors.Count + methods.Count + (2 * properties.Count) + fields.Count; // skip constructor, flags, methods and fields
            foreach (TypeReference tr in extends)
            {
                if (tr == type)
                    return i;
                ++i;
            }
            Require.NotCalled();
            return -1;
        }

        public override string ToString()
        {
            return name.Data;
        }

        private Method FindImplicitConversion(Definition from, Definition to)
        {
            Require.True((from == this) || (to == this));
            Method best = null;
            int distance = int.MaxValue;
            foreach (Method method in LocalMethods)
                if (method.ImplicitConverter)
                {
                    if (method.ReturnType.Supports(to.TypeReference)
                        && to.TypeReference.Supports(method.ReturnType)
                        && method.Parameters.ParameterList[0].TypeReference.Supports(from.TypeReference))
                    {
                        int d = method.Parameters.ParameterList[0].TypeReference.Distance(from.TypeReference);
                        if (d < distance)
                            best = method;
                    }
                }
            return best;
        }

        public bool SupportsImplicitConversion(Definition from, Definition to)
        {
            return FindImplicitConversion(from, to) != null;
        }

        public void CallImplicitConversion(Generator generator, Definition from, Definition to)
        {
            Method method = FindImplicitConversion(from, to);
            Require.Assigned(method);
            JumpToken skipNull = generator.Assembler.CreateJumpToken();
            generator.Assembler.JumpIfUnassigned(skipNull);
            generator.Assembler.PushValue();
            generator.Assembler.SetImmediateValue(RuntimeStruct, 0);
            int offset = GetMethodOffset(this, method, generator.Resolver.CurrentDefinition);
            generator.Assembler.FetchMethod(offset);
            generator.Assembler.PushValue();
            generator.Assembler.PeekValue(1);
            generator.Assembler.PushValue();
            generator.Assembler.CallFromStack(1);
            generator.Assembler.DropStackTop();
            generator.Assembler.SetDestination(skipNull);
        }
    }
}
