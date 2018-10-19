using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.WinPE32X86
{
    public class Importer : Compiler.Importer
    {
        private Dictionary<string, ImportTable> imports = new Dictionary<string, ImportTable>();
        private Writer writer;
        private Symbols symbols;
        private Region directoryTable;
        private Region trampolineRegion;
        private Region nameTable;
        private Region importAddressTable;

        public int ImportAddressTableMemoryLocation { get { return (int)importAddressTable.MemoryLocation; } }
        public int ImportAddressTableMemorySize
        {
            get
            {
                int total = importAddressTable.Length;
                foreach (ImportTable importTable in imports.Values)
                    total += importTable.importAddressTable.Length;
                return total;
            }
        }

        public Importer(Writer writer, Symbols symbols)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            if (symbols == null)
                throw new ArgumentNullException("symbols");
            this.writer = writer;
            this.symbols = symbols;
            directoryTable = writer.AllocateRegion(".idata");
            trampolineRegion = writer.AllocateRegion(".text");
            nameTable = writer.AllocateRegion(".idata");

            // this is allocated last so that it can be used as a starting point of the actual iat
            importAddressTable = writer.AllocateRegion(".idata");
            importAddressTable.MarkEmpty();
        }

        public override Placeholder FetchImport(string namespaceName, string className, string fieldName)
        {
            return FetchImportAsPointer(namespaceName, (className + "__" + fieldName).Replace('.', '_'));
        }

        private ImportTable FetchImport(string library, string entryPoint)
        {
            ImportTable importTable;
            if (!imports.TryGetValue(library, out importTable))
            {
                importTable.library = library;
                importTable.importAddressTable = writer.AllocateRegion(".idata");

                importTable.trampolines = new Dictionary<string, Placeholder>();
                importTable.importAddressTableEntries = new Dictionary<string, Placeholder>();
                imports[library] = importTable;
                directoryTable.WritePlaceholderRelative(importTable.importAddressTable.BaseLocation); // 0 Import Lookup Table (fallback on importAddressTable)
                directoryTable.WriteInt32(0);
                directoryTable.WriteInt32(0);
                directoryTable.WritePlaceholderRelative(nameTable.CurrentLocation);
                if (!library.EndsWith(".dll"))
                    library = library.Replace('.', '_') + ".dll";
                nameTable.WriteAsUtf8NullTerminated2(library);
                directoryTable.WritePlaceholderRelative(importTable.importAddressTable.BaseLocation);
            }
            if (!importTable.trampolines.ContainsKey(entryPoint))
            {
                importTable.trampolines[entryPoint] = trampolineRegion.CurrentLocation;
                importTable.importAddressTableEntries[entryPoint] = importTable.importAddressTable.CurrentLocation;

                Placeholder ph = trampolineRegion.CurrentLocation;
                trampolineRegion.WriteByte(0xFF);
                trampolineRegion.WriteByte(0x25);
                trampolineRegion.WritePlaceholder(importTable.importAddressTable.CurrentLocation);
                trampolineRegion.WriteByte(0x90);
                trampolineRegion.WriteByte(0x90);
                symbols.WriteCode(ph, trampolineRegion.CurrentLocation.MemoryDistanceFrom(ph), "trampoline:" + library + ":" + entryPoint);

                importTable.importAddressTable.WritePlaceholderRelative(nameTable.CurrentLocation);
                nameTable.WriteByte(0); // Hint
                nameTable.WriteByte(0);
                nameTable.WriteAsUtf8NullTerminated2(entryPoint);
            }
            return importTable;
        }

        public override Placeholder FetchImportAsPointer(string library, string entryPoint)
        {
            ImportTable importTable = FetchImport(library, entryPoint);
            return importTable.importAddressTableEntries[entryPoint];
        }

        public void Commit()
        {
            // Emptry trailing Director table
            directoryTable.WriteInt32(0);
            directoryTable.WriteInt32(0);
            directoryTable.WriteInt32(0);
            directoryTable.WriteInt32(0);
            directoryTable.WriteInt32(0);
            foreach (System.Collections.Generic.KeyValuePair<string, ImportTable> kvp in imports)
            {
                kvp.Value.importAddressTable.WriteInt32(0);
                symbols.WriteData(kvp.Value.importAddressTable.BaseLocation, kvp.Value.importAddressTable.Length, "iat:" + kvp.Key);
            }

            symbols.WriteData(directoryTable.BaseLocation, directoryTable.Length, ":importAddressTable:directoryTable");
            symbols.WriteData(nameTable.BaseLocation, nameTable.Length, ":importAddressTable:nameTable");
        }

        // not supported for windows
        public override void WriteGlobalSymbol(string name, Placeholder location)
        {
            throw new NotImplementedException();
        }
    }

    struct ImportTable
    {
        public string library;
        public Region importAddressTable;
        public Dictionary<string, Placeholder> importAddressTableEntries;
        public Dictionary<string, Placeholder> trampolines;
    }
}
