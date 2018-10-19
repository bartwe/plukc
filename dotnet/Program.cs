using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;
using System.IO;

namespace Compiler
{
    class Program
    {
        const string version = "0.1";
        static public bool Windows { get { return Windows_x86 || Windows_x86_64; } }
        static public bool Linux { get { return Linux_x86 || Linux_x86_64; } }
        static bool caseSensitive = Environment.OSVersion.Platform == PlatformID.Unix;
        static public bool CaseSensitiveFileSystem { get { return caseSensitive; } }
        private static bool windows_x86;
        private static bool linux_x86;
        private static bool windows_x86_64;
        private static bool linux_x86_64;
        static public bool Windows_x86 { get { return windows_x86; } }
        static public bool Linux_x86 { get { return linux_x86; } }
        static public bool Windows_x86_64 { get { return windows_x86_64; } }
        static public bool Linux_x86_64 { get { return linux_x86_64; } }
        private static bool allowUnreadAndUnusedVariablesFieldsAndExpressions;
        static public bool AllowUnreadAndUnusedVariablesFieldsAndExpressions { get { return allowUnreadAndUnusedVariablesFieldsAndExpressions; } }
        static bool stackTrace = false;
        static bool suppressPathInErrors = false;
        static public bool SuppressPathInErrors { get { return suppressPathInErrors; } }
        static bool writeFileNameOnRead = false;
        static public bool WriteFileNameOnRead { get { return writeFileNameOnRead; } }

        static int Main(string[] args)
        {
            try
            {
                List<string> types = new List<string>();
                Set<string> paths = new Set<string>();
                List<string> files = new List<string>();
                Set<string> hiddenPaths = new Set<string>();

                bool noDefaultPaths = false;
                bool path = false;
                bool noMoreFlags = false;
                bool showPathAndFiles = false;
                bool hiddenPath = false;
                bool breakOnStart = false;
                bool showUsedTypes = false;
                bool noEntryPoint = false;
                foreach (string s in args)
                {
                    bool file = path || noMoreFlags;

                    if (!file)
                    {
                        if (s == "-m:l64")
                        {
                            if (Windows_x86 || Linux_x86 || Windows_x86_64 || Linux_x86_64)
                                throw new Exception("Only one platform can be used at a time.");
                            linux_x86_64 = true;
                        }
                        else if (s == "-m:l32")
                        {
                            if (Windows_x86 || Linux_x86 || Windows_x86_64 || Linux_x86_64)
                                throw new Exception("Only one platform can be used at a time.");
                            linux_x86 = true;
                        }
                        else if (s == "-m:w64")
                        {
                            if (Windows_x86 || Linux_x86 || Windows_x86_64 || Linux_x86_64)
                                throw new Exception("Only one platform can be used at a time.");
                            windows_x86_64 = true;
                        }
                        else if (s == "-m:w32")
                        {
                            if (Windows_x86 || Linux_x86 || Windows_x86_64 || Linux_x86_64)
                                throw new Exception("Only one platform can be used at a time.");
                            windows_x86 = true;
                        }
                        else if (s == "-p")
                            path = true;
                        else if (s == "-ndp")
                            noDefaultPaths = true;
                        else if (s == "-x:v")
                            Console.WriteLine("Version " + version);
                        else if (s == "-x:ns")
                            stackTrace = false;
                        else if (s == "-x:s")
                            stackTrace = true;
                        else if (s == "-a:v")
                            allowUnreadAndUnusedVariablesFieldsAndExpressions = true;
                        else if (s == "-x:nf")
                            suppressPathInErrors = true;
                        else if (s == "-x:p")
                            showPathAndFiles = true;
                        else if (s == "-x:f")
                            writeFileNameOnRead = true;
                        else if (s == "-x:fnc")
                            caseSensitive = false;
                        else if (s == "-x:fc")
                            caseSensitive = true;
                        else if (s == "-x:b")
                            breakOnStart = true;
                        else if (s == "-x:t")
                            showUsedTypes = true;
                        else if (s == "-x:e")
                            noEntryPoint = true;
                        else if (s == "-p:h")
                        {
                            path = true;
                            hiddenPath = true;
                        }
                        else if (s == "--")
                            noMoreFlags = true;
                        else
                            file = true;
                    }

                    if (file)
                    {
                        if (path)
                        {
                            if (hiddenPath)
                                hiddenPaths.Put(Path.GetFullPath(s));
                            else
                                paths.Put(Path.GetFullPath(s));
                        }
                        else
                            if (s.Contains(".pluk"))
                                files.Add(Path.GetFullPath(s));
                            else
                                types.Add(s);
                        path = false;
                        hiddenPath = false;
                    }
                }

                if (!noDefaultPaths)
                {
                    string env_pluk_source = Environment.GetEnvironmentVariable("PLUK_SOURCES");
                    if (!string.IsNullOrEmpty(env_pluk_source))
                        paths.Put(Path.GetFullPath(env_pluk_source));
                    paths.Put(Path.GetFullPath("."));
                    foreach (string p in hiddenPaths)
                        paths.Put(p);
                }

                if (showPathAndFiles)
                {
                    foreach (string type in types)
                        Console.WriteLine("type: " + type);
                    foreach (string file in files)
                        Console.WriteLine("file: " + file);
                    foreach (string vpath in paths)
                        Console.WriteLine("path: " + vpath);
                }

                if (!(Windows_x86 || Linux_x86 || Windows_x86_64 || Linux_x86_64))
                    if (Environment.OSVersion.Platform == PlatformID.Unix)
                    {
                        if (IntPtr.Size == 8)
                            linux_x86_64 = true;
                        else if (IntPtr.Size == 4)
                            linux_x86 = true;
                        else
                            throw new ArgumentOutOfRangeException("unsupported pointer width: " + IntPtr.Size);
                    }
                    else
                    {
                        //                        if (IntPtr.Size == 8)
                        //                            windows_x86_64 = true;
                        //                        else if (IntPtr.Size == 4)
                        windows_x86 = true;
                        //                        else
                        //                            throw new ArgumentOutOfRangeException("unsupported pointer width: " + IntPtr.Size);
                    }

                if ((types.Count == 0) && (files.Count == 0))
                    throw new ArgumentException(string.Format(Resource.Culture, Resource.CompilerUsage, AppDomain.CurrentDomain.FriendlyName));

                types.Add("pluk.base.String");
                types.Add("pluk.base.Int");
                types.Add("pluk.base.Float");
                types.Add("pluk.base.Bool");
                types.Add("pluk.base.StaticString");
                types.Add("pluk.base.Object");
                types.Add("pluk.base.Application");
                types.Add("pluk.base.OverflowException");
                types.Add("pluk.base.BoundsException");

                if (paths.Count == 0)
                    paths.Add(Path.GetFullPath("."));

                Generator generator = null;
                if (Linux_x86_64)
                    generator = new Compiler.Binary.LinuxELF64X86_64.Generator(paths, !CaseSensitiveFileSystem);
                if (Linux_x86)
                    generator = new Compiler.Binary.LinuxELF32X86.Generator(paths, !CaseSensitiveFileSystem);
                if (Windows_x86)
                    generator = new Compiler.Binary.WinPE32X86.Generator(paths, !CaseSensitiveFileSystem);
                Require.Assigned(generator);
                try
                {

                    string baseClass = null;

                    if (files.Count == 0)
                        baseClass = types[0];

                    foreach (string file in files)
                    {
                        string typename = generator.Resolver.ParseFile(file);
                        if (baseClass == null)
                            baseClass = typename;
                    }

                    generator.SetModuleName(baseClass);
                    foreach (string type in types)
                    {
                        TypeName tn = new TypeName(new Identifier(new NowhereLocation(), type));
                        tn.SetHasNamespace();
                        generator.Resolver.ResolveType(new NowhereLocation(), tn);
                    }

                    TypeName baseClassType = new TypeName(new Identifier(new NowhereLocation(), baseClass));
                    baseClassType.SetHasNamespace();

                    if (noEntryPoint)
                    {
                        ResolveBaseTypes(generator);
                        generator.Resolver.ResolveEverything(generator);
                        generator.Resolver.PrepareEverything(generator);
                        generator.Resolver.GenerateEverything(generator);
                    }
                    else
                    {
                        ResolveBaseTypes(generator);
                        generator.Resolver.ResolveEverything(generator);
                        Definition t = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), baseClassType).Definition;
                        generator.Resolver.PrepareEverything(generator);
                        generator.Resolver.GenerateEverything(generator);

                        CheckHelper.SetupExceptionHandlers(generator);

                        DefinitionTypeReference applicationType = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Application")));

                        if (!t.Supports(applicationType))
                            throw new CompilerException(t, string.Format(Resource.Culture, Resource.ClassHasNoMain, baseClass));
                        generator.AllocateAssembler();
                        Assembler g = generator.Assembler;
                        if (breakOnStart)
                            g.Break();
                        g.StackRoot();
                        g.CallNative(generator.SaveStackRoot, 1, false, false);
                        g.StartFunction();
                        // setup basic types in pluk_base.dll
                        SetupBaseTypes(generator);
                        //reset floating point mask
                        generator.Assembler.SetupFpu();
                        generator.Resolver.CallStaticInitializers(generator);
                        //Instance program object and call the main method
                        g.LoadMethodStruct(t.FindConstructor(new NowhereLocation(), new FunctionTypeReference(new NowhereLocation(), t.TypeReference, new List<TypeReference>()), applicationType.Definition).RuntimeStruct);
                        g.PushValue();
                        Placeholder retSite = g.CallFromStack(0);
                        generator.AddCallTraceEntry(retSite, new NowhereLocation(), "", "entrypoint");
                        Method m = t.FindMethod(new Identifier(new NowhereLocation(), "MainIndirect"), false, null, applicationType.Definition, true);
                        g.FetchMethod(t.GetMethodOffset(new NowhereLocation(), m, applicationType.Definition));
                        g.PushValue();
                        retSite = g.CallFromStack(0);
                        generator.AddCallTraceEntry(retSite, new NowhereLocation(), "", "entrypoint");
                        // todo, provide argument
                        g.Empty();
                        g.PushValue();
                        g.PopNativeArgument();
                        g.CallNative(generator.Exit, 2, false, false);
                        g.StopFunction();
                        generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "entryPoint:" + baseClass + ".Main");
                        generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "_start");
                        generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "main");
                        generator.WriteToFile(g.Region);
                    }

                    if (showUsedTypes)
                    {
                        foreach (Definition def in generator.Definitions)
                            Console.WriteLine("Definition: " + def.Name.DataModifierLess);
                    }
                }
                finally
                {
                    generator.Dispose();
                }
            }
            catch (CompilerException ce)
            {
                if (stackTrace)
                    Console.Error.WriteLine(ce.ToString());
                else if (suppressPathInErrors)
                    Console.Error.WriteLine(ce.Error);
                else
                    Console.Error.WriteLine(ce.ErrorMessage);
                return 1;
            }
            catch (Exception e)
            {
                if (stackTrace)
                    Console.Error.WriteLine(e.ToString());
                else
                    Console.Error.WriteLine(e.Message);
                return 255;
            }
            return 0;
        }

        public static void Warn(CompilerException ce)
        {
            if (stackTrace)
                Console.Error.WriteLine(ce.ToString());
            else if (suppressPathInErrors)
                Console.Error.WriteLine(ce.Error);
            else
                Console.Error.WriteLine(ce.WarningMessage);
        }

        private static void ResolveBaseTypes(Generator generator)
        {
            TypeName classType;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Bool"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Byte"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Int"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Float"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.String"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.StaticString"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Type"));
            generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType);
        }

        private static void SetupBaseTypes(Generator generator)
        {
            TypeName classType;
            Definition definition;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Bool"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder boolType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Byte"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder byteType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Int"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder intType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Float"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder floatType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.String"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder stringType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.StaticString"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder staticStringType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.Type"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder typeType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.OverflowException"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder overflowType = definition.RuntimeStruct;
            classType = new TypeName(new Identifier(new NowhereLocation(), "pluk.base.BoundsException"));
            definition = generator.Resolver.ResolveDefinitionType(new NowhereLocation(), classType).Definition;
            Placeholder boundsType = definition.RuntimeStruct;
            generator.Assembler.CallBuildIn(generator.Setup, new Placeholder[] { boolType, byteType, intType, floatType, stringType, staticStringType, typeType, generator.CallStackData, generator.Statics.BaseLocation, generator.Statics.CurrentLocation, overflowType, boundsType });
        }
    }
}
