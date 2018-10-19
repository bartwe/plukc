using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Binary.LinuxELF;

namespace Compiler.Binary.LinuxELF32X86
{
    public class Generator : Compiler.Generator, IDisposable
    {
        private Compiler.Binary.LinuxELF.Importer importer;
        private Symbols symbols;
        private Linker linker;
        private Sections sections;
        private StringTable stringTable;
        private Region stackTraceData;
        private Region statics;

        private Region textRegion;
        private Dictionary<string, Placeholder> textData = new Dictionary<string, Placeholder>();

        public Generator(IEnumerable<string> paths, bool ignoreFileCase)
            : base(paths, ignoreFileCase)
        {
            stringTable = new StringTable();
            sections = new Sections(stringTable, false);

            Linker.RegisterSections(sections);

            symbols = new Compiler.Binary.LinuxELF.Symbols(sections, stringTable);
            importer = new Compiler.Binary.LinuxELF.Importer(sections);

            stackTraceData = AllocateDataRegion();
            callStack = stackTraceData.CurrentLocation;

            statics = sections.GetSection(".data").AllocateRegion();

            SetExternals(
                   importer.FetchImportAsPointer("pluk.base", "pluk_allocateGC"),
                   importer.FetchImportAsPointer("pluk.base", "pluk_touchGC"),
                   importer.FetchImportAsPointer("pluk.base", "pluk_disposeGC"),
                   importer.FetchImportAsPointer("pluk.base", "pluk_base_exit"),
                   importer.FetchImportAsPointer("pluk.base", "pluk_base_setup"),
                   importer.FetchImportAsPointer("pluk.base", "pluk_base_saveStackRoot")
                   );
            SetupExceptions();
        }

        public override Region Statics
        {
            get
            {
                return statics;
            }
        }

        public override void SetModuleName(string moduleName)
        {
            linker = new Linker(moduleName, sections, stringTable, false);
        }

        public override Compiler.Importer Importer
        {
            get { return importer; }
        }

        public override Compiler.Symbols Symbols
        {
            get { return symbols; }
        }

        public override Region AllocateDataRegion()
        {
            return sections.GetSection(".rodata").AllocateRegion();
        }

        public override void WriteToFile(Region entryPoint)
        {
            stackTraceData.WriteNumber(0);
            Require.Assigned(entryPoint);
            symbols.Close();
            linker.SetEntryPoint(entryPoint);
            importer.Close();
            linker.SaveToFile();
        }

        public override Placeholder AddTextLengthPrefix(string text)
        {
            if (textData.ContainsKey(text))
                return textData[text];

            if (textRegion == null)
                textRegion = AllocateDataRegion();
            Placeholder result = textRegion.CurrentLocation;
            byte[] bytes = (new System.Text.UTF8Encoding()).GetBytes(text);
            textRegion.WriteNumber(bytes.Length);
            textRegion.Write(bytes);
            textRegion.WriteByte(0);
            textRegion.Align(16, 0);
            textData[text] = result;
            return result;
        }

        public override void AddCallTraceEntry(Placeholder retPointer, ILocation location, string definition, string method)
        {
            stackTraceData.WritePlaceholder(retPointer);
            stackTraceData.WriteNumber(location.Line);
            stackTraceData.WritePlaceholder(AddTextLengthPrefix(location.Source));
            stackTraceData.WritePlaceholder(AddTextLengthPrefix(definition));
            stackTraceData.WritePlaceholder(AddTextLengthPrefix(method));
        }

        protected override Compiler.Assembler InnerAllocateAssembler()
        {
            return new AssemblerX86(sections.GetSection(".text").AllocateRegion(), true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (linker != null)
                {
                    linker.Dispose();
                    linker = null;
                }
            }
            base.Dispose(disposing);
        }

    }
}
