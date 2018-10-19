using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.WinPE32X86
{
    public class Generator : Compiler.Generator, IDisposable
    {
        private Importer importer;
        private Symbols symbols;
        private Writer writer;
        private Linker linker;
        private Region textRegion;
        private Region stackTraceData;
        private Dictionary<string, Placeholder> textData = new Dictionary<string, Placeholder>();
        private Region statics;

        public Generator(IEnumerable<string> paths, bool ignoreFileCase)
            : base(paths, ignoreFileCase)
        {
            writer = new Writer();
            writer.RegisterSection(".text", 1);
            writer.RegisterSection(".data", 2);
            writer.RegisterSection(".idata", 3);
            writer.RegisterSection(".rsrc", 4);
            writer.RegisterSection(".debug", 5);

            symbols = new Symbols(writer.DebugLocations, writer.DebugSymbols);
            importer = new Importer(writer, symbols);
            stackTraceData = AllocateDataRegion();
            callStack = stackTraceData.CurrentLocation;

            statics = writer.AllocateRegion(".data");

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
            linker = new Linker(writer, importer, moduleName);
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
            return writer.AllocateRegion(".data");
        }

        public override void WriteToFile(Region entryPoint)
        {
            stackTraceData.WriteNumber(0);
            symbols.Close();
            linker.Process(entryPoint);
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
            return new AssemblerX86(writer.AllocateRegion(".text"), false);
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
                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
