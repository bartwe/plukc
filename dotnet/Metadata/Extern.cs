using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.IO;

namespace Compiler.Metadata
{
    public class Extern : NodeBase
    {
        private string xml;

        private string library;
        private string entrypoint;
        private string returns;
        private List<string> parameters = new List<string>();

        private DefinitionTypeReference intType;
        private TypeReference voidType;

        public Extern(ILocation location)
            : base(location)
        {
        }

        public Extern(ParserToken token)
            : base(token)
        {
            xml = token.Token;
            xml = StringLiteralExpression.Unescape(this, xml);
            if (!string.IsNullOrEmpty(xml))
            {
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = true;
                doc.LoadXml(xml);
                XmlNode root = doc.FirstChild;
                if (Program.Windows_x86)
                    root = root.SelectSingleNode("/interop/win32/spec");
                if (Program.Windows_x86_64)
                    root = root.SelectSingleNode("/interop/win64/spec");
                if (Program.Linux_x86)
                    root = root.SelectSingleNode("/interop/lin32/spec");
                if (Program.Linux_x86_64)
                    root = root.SelectSingleNode("/interop/lin64/spec");
                Require.Assigned(root);
                XmlNode value;
                value = root.Attributes["library"];
                library = (value != null) ? value.Value : "";
                value = root.Attributes["entrypoint"];
                entrypoint = (value != null) ? value.Value : "";
                value = root.Attributes["returns"];
                returns = (value != null) ? value.Value : "void";
                foreach (XmlNode parameter in root.SelectNodes("./parameter"))
                {
                    Require.Assigned(parameter);
                    value = parameter.Attributes["type"];
                    parameters.Add(value.Value);
                }
            }
        }

        public void Resolve(Generator generator)
        {
            intType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Int")));
            voidType = generator.Resolver.ResolveType(this, new TypeName(new Identifier(this, "void")));
        }

        public Placeholder Generate(Generator generator, Identifier primaryContextName, Identifier methodName, TypeReference returnType, Parameters parametersMetadata)
        {
            if (string.IsNullOrEmpty(xml))
            {
                generator.AllocateAssembler();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, methodName);
                generator.Assembler.CallNative(generator.Importer.FetchImport(primaryContextName.Namespace, primaryContextName.Data, methodName.Data), 0, false, true);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, methodName, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "extern:" + primaryContextName.Data + "." + methodName.Data);
                return generator.Assembler.Region.BaseLocation;
            }
            else
            {
                generator.AllocateAssembler();
                parametersMetadata.Generate(generator);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, methodName);
                generator.Assembler.StartFunction();

                if (parametersMetadata.ParameterList.Count != parameters.Count)
                    throw new ExternException(this, "Parameter count mismatch.");

                string word = "int32";
                if (Program.Linux_x86_64 || Program.Windows_x86_64)
                    word = "int64";

                generator.Assembler.SetupNativeReturnSpace();

                for (int i = parameters.Count - 1; i >= 0; --i)
                {
                    string type = parameters[i];
                    if (type == word)
                    {
                        generator.Assembler.RetrieveVariable(parametersMetadata.ParameterList[i].Slot);
                        generator.Assembler.PushValuePart();
                    }
                    else
                        throw new ExternException(this, "Unsupported type.");
                }

                if (string.IsNullOrEmpty(library))
                    throw new ExternException(this, "No library specified");
                if (string.IsNullOrEmpty(entrypoint))
                    throw new ExternException(this, "No entrypoint specified");
                generator.Assembler.CallNative(generator.Importer.FetchImportAsPointer(library, entrypoint), parameters.Count, false, false);

                if (returns == word)
                {
                    generator.Assembler.SetTypePart(intType.RuntimeStruct);
                    returnType.GenerateConversion(this, generator, intType);
                }
                else
                    if (returns == "void")
                    {
                        voidType.GenerateConversion(this, generator, returnType);
                        generator.Assembler.Empty();
                    }
                    else
                        throw new ExternException(this, "Unsupported return type.");

                generator.Assembler.StopFunction();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, methodName, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "extern:" + primaryContextName.Data + "." + methodName.Data);
                return generator.Assembler.Region.BaseLocation;
            }
        }
    }
}
