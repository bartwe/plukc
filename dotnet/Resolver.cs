using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace Compiler
{
    public class Resolver : IDisposable
    {
        public class Context
        {
            public class Entry
            {
                public string id;
                public Identifier name;
                public int slot;
                public TypeReference type;
                public bool readOnly;
            }

            public Dictionary<Field, bool> fieldWritten = new Dictionary<Field, bool>();
            public Dictionary<string, Entry> variables = new Dictionary<string, Entry>();
            public Dictionary<int, Entry> varBySlot = new Dictionary<int, Entry>();
            public Dictionary<int, bool> variableIncomplete = new Dictionary<int, bool>();
            public Dictionary<int, bool> variableWrite = new Dictionary<int, bool>();
            public Dictionary<int, bool> variableRead = new Dictionary<int, bool>();
            public Dictionary<int, bool> variableUsed = new Dictionary<int, bool>();
            public Context parent;
            public bool parentReadOnly;
            public ImplicitField implicitSlot;
            public Dictionary<string, JumpToken> gotos = new Dictionary<string, JumpToken>();
            public bool tryContext;
            public Parameters contextParameters;

            public Context()
            {
            }

            public void Setup(Context parent, bool parentReadOnly)
            {
                this.parent = parent;
                this.parentReadOnly = parentReadOnly;
                if (parentReadOnly)
                    Require.Assigned(parent);
            }

            public void Reset()
            {
                fieldWritten.Clear();
                variables.Clear();
                varBySlot.Clear();
                variableIncomplete.Clear();
                variableWrite.Clear();
                variableRead.Clear();
                variableUsed.Clear();
                parent = null;
                implicitSlot = null;
                gotos.Clear();
                tryContext = false;
                contextParameters = null;
            }

            public void CheckEmpty()
            {
                foreach (Field field in fieldWritten.Keys)
                    Require.True(field.GetModifiers.Static);
                Require.True(variables.Count == 0);
                Require.True(varBySlot.Count == 0);
                Require.True(variableIncomplete.Count == 0);
                Require.True(variableWrite.Count == 0);
                Require.True(variableRead.Count == 0);
            }

            public void AddVariable(Identifier name, TypeReference type, int slot, bool readOnly)
            {
                Require.Assigned(name);
                Require.Assigned(type);
                if (variables.ContainsKey(name.Data))
                    throw new CompilerException(name, string.Format(Resource.Culture,
                        Resource.VariableWithThatNameAlreadyDefined, name.Data));
                Entry e = new Entry();
                e.id = name.Data;
                e.name = name;
                e.slot = slot;
                e.type = type;
                e.readOnly = readOnly;
                variables.Add(name.Data, e);
                varBySlot.Add(slot, e);
            }

            public void SetImplicitFields(int slot, TypeReference type)
            {
                Require.Assigned(type);
                Require.Unassigned(implicitSlot);
                implicitSlot = new ImplicitField();
                implicitSlot.slot = slot;
                implicitSlot.type = type;
                AssignSlot(slot);
                ReadSlot(slot);
            }

            public void AssignField(Field field)
            {
                fieldWritten[field] = true;
            }

            public bool IsFieldAssigned(Field field)
            {
                return fieldWritten.ContainsKey(field);
            }

            public void AssignSlot(int slot)
            {
                variableWrite[slot] = true;
            }

            public void WriteSlot(ILocation location, int slot)
            {
                Context c = this;
                Entry e = null;
                bool readOnly = false;
                while (c != null)
                {
                    if (c.varBySlot.TryGetValue(slot, out e))
                    {
                        readOnly |= e.readOnly;
                        break;
                    }
                    e = null;
                    readOnly |= c.parentReadOnly;
                    c = c.parent;
                }
                if (e == null)
                    throw new CompilerException(location, "Internal compiler error, failed to resolve slot for marked as written: " + slot);
                if (readOnly)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.CannotAssignReadOnlyVar, e.name.Data));
                variableWrite[slot] = true;
            }

            public void IncompleteSlot(int slot, bool complete)
            {
                variableRead.Remove(slot);
                variableWrite.Remove(slot);
                variableIncomplete[slot] = complete;
            }

            public void ReadSlot(int slot)
            {
                variableRead[slot] = true;
            }

            public void Close()
            {
                if (Program.AllowUnreadAndUnusedVariablesFieldsAndExpressions)
                    return;
                foreach (Entry e in variables.Values)
                {
                    if (variableIncomplete.ContainsKey(e.slot))
                        continue;
                    if (!variableWrite.ContainsKey(e.slot))
                    {
                        if (!variableUsed.ContainsKey(e.slot))
                            throw new CompilerException(e.name, string.Format(Resource.Culture, Resource.UnusedVariable, e.name.Data));
                    }
                    else if (!variableRead.ContainsKey(e.slot))
                        throw new CompilerException(e.name, string.Format(Resource.Culture, Resource.VariableAssignedButUnused, e.name.Data));
                }
            }

            public void Merge(Context context)
            {
                foreach (Entry v in context.variables.Values)
                {
                    context.variableWrite.Remove(v.slot);
                    context.variableRead.Remove(v.slot);
                }
                foreach (KeyValuePair<int, bool> e in context.variableWrite)
                {
                    variableWrite[e.Key] = e.Value;
                    variableUsed[e.Key] = true;
                }
                foreach (KeyValuePair<int, bool> e in context.variableRead)
                {
                    variableRead[e.Key] = e.Value;
                    variableUsed[e.Key] = true;
                }
                foreach (KeyValuePair<Field, bool> e in context.fieldWritten)
                    fieldWritten[e.Key] = e.Value;
                foreach (int key in context.variableUsed.Keys)
                    variableUsed[key] = true;
            }

            public void MergeReads(Context context)
            {
                foreach (Entry v in context.variables.Values)
                {
                    context.variableRead.Remove(v.slot);
                }
                foreach (Entry v in context.variables.Values)
                    context.variableRead.Remove(v.slot);
                foreach (KeyValuePair<int, bool> e in context.variableWrite)
                    variableUsed[e.Key] = true;
                foreach (KeyValuePair<int, bool> e in context.variableRead)
                {
                    variableRead[e.Key] = e.Value;
                    variableUsed[e.Key] = true;
                }
                foreach (int key in context.variableUsed.Keys)
                    variableUsed[key] = true;
            }

            public void Intersects(Context left, Context right)
            {
                foreach (Entry v in left.variables.Values)
                {
                    left.variableWrite.Remove(v.slot);
                    left.variableRead.Remove(v.slot);
                }
                foreach (Entry v in right.variables.Values)
                {
                    right.variableWrite.Remove(v.slot);
                    right.variableRead.Remove(v.slot);
                }
                foreach (KeyValuePair<int, bool> e in left.variableRead)
                    variableRead[e.Key] = e.Value;
                foreach (KeyValuePair<int, bool> e in right.variableRead)
                    variableRead[e.Key] = e.Value;
                foreach (KeyValuePair<int, bool> e in left.variableWrite)
                {
                    if (right.variableWrite.ContainsKey(e.Key))
                        variableWrite[e.Key] = true;
                }
                foreach (KeyValuePair<Field, bool> e in left.fieldWritten)
                {
                    if (right.fieldWritten.ContainsKey(e.Key))
                        fieldWritten[e.Key] = true;
                }
            }

            public void RegisterTryContext()
            {
                Require.False(tryContext);
                tryContext = true;
            }

            public void RegisterGoto(string token, JumpToken gotoToken)
            {
                Require.False(gotos.ContainsKey(token));
                gotos.Add(token, gotoToken);
            }

            public void SetContextParameters(Parameters parameters)
            {
                Require.Assigned(parameters);
                Require.Unassigned(contextParameters);
                contextParameters = parameters;
            }
        }

        private DefinitionCollection store;
        private Stack<Context> contexts = new Stack<Context>();
        private Set<Definition> resolvedDefinitions = new Set<Definition>();
        private List<string> paths;
        private bool finisedResolving;
        private Set<string> imports = new Set<string>();
        private Dictionary<string, TypeReference> resolveCache = new Dictionary<string, TypeReference>();
        private Dictionary<string, TypeReference> localResolveCache = new Dictionary<string, TypeReference>();
        private Set<string> parsedFiles;
        private Stack<Context> recycledContexts = new Stack<Context>();
        private Prefetcher prefetcher;

        public Resolver(DefinitionCollection store, IEnumerable<string> paths, bool ignoreFileCase)
        {
            if (store == null)
                throw new ArgumentNullException("store");
            if (paths == null)
                throw new ArgumentNullException("paths");
            this.store = store;

            Set<string> dp = new Set<string>();
            foreach (string p in paths)
                dp.Put(Path.GetFullPath(p + Path.DirectorySeparatorChar));

            this.paths = new List<string>(dp);

            if (Program.CaseSensitiveFileSystem)
                parsedFiles = new Set<string>(StringComparer.Ordinal);
            else
                parsedFiles = new Set<string>(StringComparer.InvariantCultureIgnoreCase);

            SetupVoid();

            prefetcher = new Prefetcher(ignoreFileCase);
        }

        public void Dispose()
        {
            if (prefetcher != null)
            {
                prefetcher.Dispose();
                prefetcher = null;
            }
        }

        public void SetImportsContext(Set<string> imports)
        {
            this.imports = imports;
            localResolveCache.Clear();
        }

        private void LoadType(TypeName type)
        {
            if (!type.HasNamespace)
                throw new Exception("Should not load anything but namespaced types.");

            if (store.HasDefinition(type) || store.HasTemplateDefinition(type))
                return;

            if (finisedResolving)
                throw new InvalidOperationException("internal compiler error: Cannot load additional type: " + type.Data + " when the resolve phase has been completed.");

            string filename = type.PrimaryName.Data.Replace('.', Path.DirectorySeparatorChar) + ".pluk";
            Set<string> files = new Set<string>();
            foreach (string path in paths)
            {
                string fullFileName = path + filename;
                if (parsedFiles.Contains(fullFileName))
                    continue;
                prefetcher.Enqueue(fullFileName);
                files.Add(fullFileName);
            }
            while (files.Count > 0)
            {
                string fullFileName = prefetcher.FirstFromSet(files);
                files.Remove(fullFileName);
                using (StreamReader sr = prefetcher.Request(fullFileName))
                {
                    parsedFiles.Add(fullFileName);
                    if (sr != null)
                    {
                        if (Program.WriteFileNameOnRead)
                            Console.WriteLine("parsing: " + fullFileName);
                        Syntax syntax = new Syntax(store, fullFileName, sr);
                        syntax.Parse();
                    }
                }
            }
        }

        public string ParseFile(string fileName)
        {
            fileName = Path.GetFullPath(fileName);
            if (parsedFiles.Contains(fileName))
                return null;
            parsedFiles.Add(fileName);

            using (StreamReader sr = prefetcher.Request(fileName))
            {
                Syntax syntax = new Syntax(store, fileName, sr);
                return syntax.Parse();
            }
        }

        public void ResolveEverything(Generator generator)
        {
            List<Definition> toVisit = new List<Definition>();
            do
            {
                toVisit.AddRange(store);
                toVisit.RemoveAll(resolvedDefinitions.Contains);
                foreach (Definition definition in toVisit)
                {
                    definition.Resolve(generator);
                    resolvedDefinitions.Add(definition);
                }
            } while (toVisit.Count > 0);
        }

        public void PrepareEverything(Generator generator)
        {
            finisedResolving = true;
            Set<Definition> visited = new Set<Definition>();
            List<Definition> toVisit = new List<Definition>();
            do
            {
                toVisit.AddRange(store);
                toVisit.RemoveAll(visited.Contains);
                foreach (Definition definition in toVisit)
                {
                    definition.PrepareExtends();
                    visited.Add(definition);
                }
            } while (toVisit.Count > 0);
            visited.Clear();
            toVisit.Clear();
            do
            {
                toVisit.AddRange(store);
                toVisit.RemoveAll(visited.Contains);
                foreach (Definition definition in toVisit)
                {
                    definition.BeforePrepare();
                    visited.Add(definition);
                }
            } while (toVisit.Count > 0);
            visited.Clear();
            toVisit.Clear();
            do
            {
                toVisit.AddRange(store);
                toVisit.RemoveAll(visited.Contains);
                foreach (Definition definition in toVisit)
                {
                    definition.Prepare(generator);
                    visited.Add(definition);
                }
            } while (toVisit.Count > 0);
            visited.Clear();
            toVisit.Clear();
            do
            {
                toVisit.AddRange(store);
                toVisit.RemoveAll(visited.Contains);
                foreach (Definition definition in toVisit)
                {
                    definition.PrepareNestedConstructors(generator);
                    visited.Add(definition);
                }
            } while (toVisit.Count > 0);
        }

        public void GenerateEverything(Generator generator)
        {
            finisedResolving = true;
            Set<Definition> visited = new Set<Definition>();
            Set<Definition> toVisit = new Set<Definition>();
            Set<DefinitionTypeReference> extends = new Set<DefinitionTypeReference>();
            do
            {
                toVisit.Clear();
                toVisit.AddRange(store);
                toVisit.RemoveAll(visited.Contains);
                foreach (Definition definition in toVisit)
                {
                    extends.Clear();
                    definition.GetExtends(extends);
                    bool ready = true;
                    foreach (DefinitionTypeReference tr in extends)
                        if (!visited.Contains(tr.Definition))
                            ready = false;
                    if (ready)
                    {
                        definition.Generate(generator);
                        visited.Add(definition);
                    }
                }
            } while (toVisit.Count > 0);
        }

        public void CallStaticInitializers(Generator generator)
        {
            foreach (Definition definition in store.Definitions)
            {
                definition.UpdateDependsOrder();
            }
            List<Definition> sortedDefinitions = new List<Definition>(store.Definitions);
            sortedDefinitions.Sort(SortDefinitions);
            foreach (Definition def in sortedDefinitions)
                if (!def.StaticInitializerFunctionPointer.IsNull)
                    generator.Assembler.CallDirect(def.StaticInitializerFunctionPointer);
        }

        private int SortDefinitions(Definition x, Definition y)
        {
            return -x.DependsOrder.CompareTo(y.DependsOrder);
        }

        public TypeReference ResolveType(ILocation location, TypeName type)
        {
            return InnerResolveType(location, type, true);
        }

        public TypeReference TryResolveType(TypeName type)
        {
            return InnerResolveType(type, type, false);
        }

        private TypeReference InnerResolveType(ILocation location, TypeName type, bool throwIfNoMatch)
        {
            Require.Assigned(location);
            if ( type.IsFunction || !type.HasNamespace || (type.TemplateParameters.Count > 0))
            {
                if (localResolveCache.ContainsKey(type.Data))
                {
                    TypeReference t = localResolveCache[type.Data];
                    if ((t != null) || (!throwIfNoMatch))
                        return t;
                }
            }
            else
            {
                if (resolveCache.ContainsKey(type.Data))
                {
                    TypeReference t = resolveCache[type.Data];
                    if ((t != null) || (!throwIfNoMatch))
                        return t;
                }
            }
            TypeReference result;
            TypeName original = type;
            if (type.Nullable)
            {
                result = InnerResolveType(location, new TypeName(type, Nullability.NotNullable), throwIfNoMatch);
            }
            else
            {
                if (type.IsFunction)
                {
                    result = type.ResolveFunctionType(this);
                }
                else
                {
                    if (type.HasNamespace)
                    {
                        if (type.TemplateParameters.Count > 0)
                        {
                            TypeName resolvedInnerName = new TypeName(type.PrimaryName);
                            resolvedInnerName.SetHasNamespace();
                            foreach (TypeName templateParameter in type.TemplateParameters)
                            {
                                TypeReference t = InnerResolveType(templateParameter, templateParameter, throwIfNoMatch);
                                if ((!throwIfNoMatch) && (t == null)) return null;
                                if (t.TypeName.Data.StartsWith("MapBucket"))
                                {
                                    t = InnerResolveType(templateParameter, templateParameter, throwIfNoMatch);
                                    throw new Exception("boom");
                                }
                                resolvedInnerName.AddTemplateParameter(t.TypeName);
                            }
                            type = resolvedInnerName;
                        }
                        LoadType(type);
                        if ((!store.HasDefinition(type)) && store.HasTemplateDefinition(type))
                        {
                            List<TypeReference> parameters = new List<TypeReference>();
                            foreach (TypeName templateParameter in type.TemplateParameters)
                            {
                                TypeReference t = InnerResolveType(templateParameter, templateParameter, throwIfNoMatch);
                                if ((!throwIfNoMatch) && (t == null))
                                    return null;
                                parameters.Add(t);
                            }
                            store.InstantiateTemplate(type, parameters);
                        }
                        result = ResolveTypeFullyQualified(location, type, throwIfNoMatch);
                    }
                    else
                    {
                        if (type.TemplateParameters.Count > 0)
                        {
                            TypeName outerType = new TypeName(type.PrimaryName);
                            foreach (TypeName innerName in type.TemplateParameters)
                            {
                                TypeReference t = InnerResolveType(location, innerName, throwIfNoMatch);
                                if ((!throwIfNoMatch) && (t == null)) return null;
                                outerType.AddTemplateParameter(t.TypeName);
                            }
                            type = outerType;
                        }

                        if (imports.Count == 0)
                            imports.Put(""); // when no imports are found, assume baseline system
                        if (!imports.Contains("pluk.base"))
                            imports.Put("pluk.base"); // this may seem silly, bit if we come here recursively this avoids a concurrent modifiecation assertion
                        result = null;
                        foreach (string ns in imports)
                        {
                            TypeName fullname = type.Prefix(ns);
                            result = InnerResolveType(location, fullname, false);
                            if (result != null)
                            {
                                type = fullname;
                                break;
                            }
                        }
                        if (result == null)
                        {
                            if (throwIfNoMatch)
                                throw new CompilerException(location, string.Format(Resource.Culture, Resource.NoDefinitionForType, type.DataModifierLess));
                        }
                    }
                }
            }
            if (original.Nullable && (!original.IsVoid) && (result != null))
                result = new NullableTypeReference(result, new TypeName(result.TypeName, Nullability.ExplicitNullable));
            if (result != null)
                resolveCache[result.TypeName.Data] = result;
            if ((result == null) || original.IsFunction || !original.HasNamespace || (original.TemplateParameters.Count > 0))
            {
                localResolveCache[original.Data] = result;
            }
            else
                resolveCache[original.Data] = result;
            return result;
        }

        public DefinitionTypeReference ResolveDefinitionType(ILocation location, TypeName type)
        {
            if (type.Nullable)
                type = new TypeName(type, Nullability.NotNullable);
            return (DefinitionTypeReference)ResolveType(location, type);
        }

        private TypeReference ResolveTypeFullyQualified(ILocation location, TypeName type, bool throwIfNoMatch)
        {
            if ((!type.HasNamespace) || (!store.HasDefinition(type)))
            {
                if (throwIfNoMatch)
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.NoDefinitionForType, type.DataModifierLess));
                else
                    return null;
            }
            Definition definition = store.FindDefinition(type);
            return definition.TypeReference;
        }

        private string currentFieldName;
        public string CurrentFieldName { get { return currentFieldName; } set { currentFieldName = value; } }

        Definition currentDefinition;
        Definition savedDefinition;

        public Definition CurrentDefinition { get { return currentDefinition; } }

        public void EnterFakeContext(Definition definition)
        {
            Require.Assigned(definition);
            Require.Assigned(currentDefinition);
            Require.Unassigned(savedDefinition);
            savedDefinition = currentDefinition;
            currentDefinition = definition;
        }

        public void LeaveFakeContext()
        {
            Require.Assigned(currentDefinition);
            Require.Assigned(savedDefinition);
            currentDefinition = savedDefinition;
            savedDefinition = null;
        }

        private Context CreateContext()
        {
            if (recycledContexts.Count > 0)
                return recycledContexts.Pop();
            return new Context();
        }

        public void EnterDefinitionContext(Definition definition)
        {
            Require.True(contexts.Count == 0);
            Require.Unassigned(currentDefinition);
            currentDefinition = definition;
            Context context = CreateContext();
            context.Setup(null, false);
            contexts.Push(context);
        }

        public void EnterContext()
        {
            Context context = CreateContext();
            context.Setup(contexts.Peek(), false);
            contexts.Push(context);
        }

        public void EnterContextParentReadOnly()
        {
            Context context = CreateContext();
            context.Setup(contexts.Peek(), true);
            contexts.Push(context);
        }

        public void LeaveContext()
        {
            Context context = contexts.Pop();
            context.Close();
            if (contexts.Count == 0)
                currentDefinition = null;
            else
                contexts.Peek().MergeReads(context);
            ReleaseContext(context);
        }

        public Context LeaveContextAcquire()
        {
            Context context = contexts.Pop();
            context.Close();
            if (contexts.Count == 0)
                currentDefinition = null;
            else
                contexts.Peek().MergeReads(context);
            return context;
        }

        public void MergeContext(Context context)
        {
            contexts.Peek().Merge(context);
        }

        public void ReleaseContext(Context context)
        {
            if (recycledContexts.Count < 64)
            {
                context.Reset();
                recycledContexts.Push(context);
            }
        }

        public void LeaveAndMergeContext()
        {
            Context context = contexts.Pop();
            context.Close();
            if (contexts.Count == 0)
                currentDefinition = null;
            else
                contexts.Peek().Merge(context);
            ReleaseContext(context);
        }

        public void IntersectContexts(Context left, Context right)
        {
            contexts.Peek().Intersects(left, right);
        }

        public void AddVariable(Identifier name, TypeReference type, int slot, bool readOnly)
        {
            if (name.Data == "this")
                Require.True(readOnly);
            contexts.Peek().AddVariable(name, type, slot, readOnly);
        }

        public void SetContextParameters(Parameters parameters)
        {
            contexts.Peek().SetContextParameters(parameters);
        }

        public Parameters CurrentContextParameters()
        {
            foreach (Context context in contexts)
            {
                if (context.contextParameters != null)
                    return context.contextParameters;
            }
            return null;
        }

        public void RegisterTryContext()
        {
            contexts.Peek().RegisterTryContext();
        }

        public void RegisterGoto(string token, JumpToken gotoToken)
        {
            contexts.Peek().RegisterGoto(token, gotoToken);
        }

        // returns null if nosuch goto is found (or callable)
        public JumpToken FindGoto(string token, out bool tryContext)
        {
            tryContext = false;
            foreach (Context context in contexts)
            {
                JumpToken result;
                if (context.gotos.TryGetValue(token, out result))
                    return result;
                if (context.tryContext)
                {
                    tryContext = true;
                }
            }
            return null;
        }

        public IEnumerable<Field> AssignedFields
        {
            get
            {
                Set<Field> result = new Set<Field>();
                foreach (Context context in contexts)
                {
                    foreach (Field f in context.fieldWritten.Keys)
                        result.Put(f);
                }
                return result;
            }
        }

        public void SetImplicitFields(int slot, TypeReference type)
        {
            Require.True(type.IsDefinition || type.IsStatic);
            contexts.Peek().SetImplicitFields(slot, type);
        }

        public TypeReference ResolveSlotType(Identifier name)
        {
            return FindFirstContextContaining(name).variables[name.Data].type;
        }

        public int ResolveSlotOffset(Identifier name)
        {
            return FindFirstContextContaining(name).variables[name.Data].slot;
        }

        public bool ContainsSlot(Identifier name)
        {
            foreach (Context context in contexts)
            {
                if (context.variables.ContainsKey(name.Data))
                    return true;
            }
            return false;
        }

        public class ImplicitField
        {
            public int slot;
            public TypeReference type;
        }

        public ImplicitField FindImplicitField(Identifier name)
        {
            foreach (Context context in contexts)
            {
                if (context.variables.ContainsKey(name.Data))
                    return null;
                if (context.implicitSlot != null)
                {
                    Definition definition;
                    bool staticRef = false;
                    if (context.implicitSlot.type is StaticTypeReference)
                    {
                        staticRef = true;
                        definition = ((StaticTypeReference)context.implicitSlot.type).Parent.Definition;
                    }
                    else
                        definition = ((DefinitionTypeReference)context.implicitSlot.type).Definition;
                    if (definition.HasField(name, staticRef) ||
                        definition.HasMethod(name, staticRef) ||
                        definition.HasProperty(name, staticRef))
                    {
                        return context.implicitSlot;
                    }
                }
            }
            return null;
        }

        private Context FindFirstContextContaining(Identifier name)
        {
            foreach (Context context in contexts)
            {
                if (context.variables.ContainsKey(name.Data))
                    return context;
            }
            throw new CompilerException(name, string.Format(Resource.Culture, Resource.FailedToResolveVariable, name.Data));
        }

        public void AssignField(Field field)
        {
            contexts.Peek().AssignField(field);
        }

        public bool IsFieldAssigned(Field field)
        {
            foreach (Context context in contexts)
                if (context.fieldWritten.ContainsKey(field))
                    return true;
            return false;
        }

        public void AssignSlot(int slot)
        {
            contexts.Peek().AssignSlot(slot);
        }

        public void CheckContextEmpty()
        {
            contexts.Peek().CheckEmpty();
        }

        public void WriteSlot(ILocation location, int slot)
        {
            contexts.Peek().WriteSlot(location, slot);
        }

        public void IncompleteSlot(int slot, bool complete)
        {
            contexts.Peek().IncompleteSlot(slot, complete);
        }

        public void RetrieveField(ILocation location, Field field)
        {
            if (currentDefinition == null)
                return;
            if (field.ParentDefinition != currentDefinition)
                return;
            if (field.GetModifiers.Static && currentDefinition.StaticFieldsInitialized)
                return;
            if (!field.GetModifiers.Static && currentDefinition.FieldsInitialized)
                return;
            foreach (Context context in contexts)
                if (context.fieldWritten.ContainsKey(field))
                    return;
            throw new CompilerException(location, string.Format(Resource.Culture, Resource.ObjectNotFullyAssigned, field.ParentDefinition.Name.Data));
        }

        public void RetrieveSlot(ILocation location, int slot, bool allowIncomplete)
        {
            bool incomplete = false;
            foreach (Context context in contexts)
                if (context.variableIncomplete.ContainsKey(slot))
                {
                    if (allowIncomplete)
                        return;
                    if (context.variableIncomplete[slot])
                        return;
                    incomplete = true;
                    break;
                }
            bool written = false;
            if (!incomplete)
            {
                foreach (Context context in contexts)
                    if (context.variableRead.ContainsKey(slot))
                        return;
                foreach (Context context in contexts)
                    if (context.variableWrite.ContainsKey(slot))
                    {
                        written = true;
                        break;
                    }
            }
            Context.Entry entry = null;
            foreach (Context context in contexts)
                foreach (Context.Entry v in context.variables.Values)
                    if (v.slot == slot)
                    {
                        entry = v;
                        break;
                    }
            foreach (Context context in contexts)
                if (context.variableIncomplete.ContainsKey(slot))
                {
                    incomplete = true;
                    break;
                }
            Require.Assigned(entry);
            if (incomplete)
            {
                if (entry.type.IsDefinition)
                {
                    Definition d = ((DefinitionTypeReference)entry.type).Definition;
                    if (d.CheckAllFieldsAssigned(location, this, true))
                    {
                        IncompleteSlot(slot, true);
                        return;
                    }
                }
                throw new CompilerException(location, string.Format(Resource.Culture, Resource.ObjectNotFullyAssigned, entry.name.Data));
            }
            else
            {
                if (written)
                {
                    contexts.Peek().ReadSlot(slot);
                }
                else
                {
                    throw new CompilerException(location, string.Format(Resource.Culture, Resource.VariableMightNotBeAssigned, entry.name.Data));
                }
            }
        }

        private void SetupVoid()
        {
            Definition definition = new Definition(new NowhereLocation(), new Set<string>(), new Modifiers(new NowhereLocation()));
            definition.SetName(new Identifier(new NowhereLocation(), "void"));
            definition.Complete();
            store.Add(definition);
        }
    }
}
