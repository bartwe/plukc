using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Compiler.Binary.WinPE32X86
{
    public class Linker : IDisposable
    {
        private MemoryStream stream;
        private MemoryStream debugStream;
        private Writer writer;
        private Importer importer;
        private Region symbols;
        private Region locations;
        private string moduleName;

        public Linker(Writer writer, Importer importer, string moduleName)
        {
            if (writer == null)
                throw new ArgumentNullException("writer");
            if (importer == null)
                throw new ArgumentNullException("importer");
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentOutOfRangeException("moduleName");

            stream = new MemoryStream(64 * 1024);
            debugStream = new MemoryStream(64 * 1024);
            this.writer = writer;
            this.importer = importer;

            symbols = writer.DebugSymbols;
            locations = writer.DebugLocations;
            this.moduleName = moduleName;
        }

        public void Process(Region entryPoint)
        {
            importer.Commit();
            WriteVersionHeader();
            WriteDebugRedirect();


            int FileAlignment = 512;
            int SectionAlignment = 4096;
            int CodeAlignment = 8;
            int DataAlignment = 8;
            int ImportAlignment = 8;
            int ResourceAlignment = 1;
            int DebugAlignment = 4;
            int ImageBase = 0x400000;

            int SizeOfHeaders = 0x240;
            int SizeOfHeadersOnDisk = Align(SizeOfHeaders, FileAlignment);
            int SizeOfHeadersInMemory = Align(SizeOfHeaders, SectionAlignment);

            int NumberOfSections = 5;

            int BaseCodeOffset = SizeOfHeadersInMemory;

            int BaseOfCodeRelativeInMemory = Align(BaseCodeOffset, SectionAlignment);
            int SizeOfCodeInMemory = Align(writer.Length(".text", CodeAlignment), SectionAlignment);
            int BaseOfCodeRelativeOnDisk = Align(SizeOfHeadersOnDisk, FileAlignment);
            int SizeOfCodeOnDisk = Align(writer.Length(".text", CodeAlignment), FileAlignment);

            int BaseOfDataRelativeInMemory = Align(BaseOfCodeRelativeInMemory + SizeOfCodeInMemory, SectionAlignment);
            int SizeOfDataInMemory = Align(writer.Length(".data", DataAlignment), SectionAlignment);
            int BaseOfDataRelativeOnDisk = Align(BaseOfCodeRelativeOnDisk + SizeOfCodeOnDisk, FileAlignment);
            int SizeOfDataOnDisk = Align(writer.Length(".data", DataAlignment), FileAlignment);

            int SizeOfImport = writer.Length(".idata", ImportAlignment);
            int BaseOfImportRelativeInMemory = Align(BaseOfDataRelativeInMemory + SizeOfDataInMemory, SectionAlignment);
            int SizeOfImportInMemory = Align(SizeOfImport, SectionAlignment);
            int BaseOfImportRelativeOnDisk = Align(BaseOfDataRelativeOnDisk + SizeOfDataOnDisk, FileAlignment);
            int SizeOfImportOnDisk = Align(SizeOfImport, FileAlignment);

            int SizeOfResource = writer.Length(".rsrc", ResourceAlignment);
            int BaseOfResourceRelativeInMemory = Align(BaseOfImportRelativeInMemory + SizeOfImportInMemory, SectionAlignment);
            int SizeOfResourceInMemory = Align(SizeOfResource, SectionAlignment);
            int BaseOfResourceRelativeOnDisk = Align(BaseOfImportRelativeOnDisk + SizeOfImportOnDisk, FileAlignment);
            int SizeOfResourceOnDisk = Align(SizeOfResource, FileAlignment);

            int SizeOfDebug = writer.Length(".debug", DebugAlignment);
            int BaseOfDebugRelativeInMemory = Align(BaseOfResourceRelativeInMemory + SizeOfResourceInMemory, SectionAlignment);
            int SizeOfDebugInMemory = Align(SizeOfDebug, SectionAlignment);
            int BaseOfDebugRelativeOnDisk = Align(BaseOfResourceRelativeOnDisk + SizeOfResourceOnDisk, FileAlignment);
            int SizeOfDebugOnDisk = Align(SizeOfDebug, FileAlignment);

            int SizeOfImageInMemory = SizeOfHeadersInMemory + SizeOfDataInMemory + SizeOfCodeInMemory + SizeOfImportInMemory + SizeOfResourceInMemory + SizeOfDebugInMemory;

            int VABaseCode = ImageBase + BaseOfCodeRelativeInMemory;
            int VABaseData = ImageBase + BaseOfDataRelativeInMemory;
            int VABaseImport = ImageBase + BaseOfImportRelativeInMemory;
            int VABaseResource = ImageBase + BaseOfResourceRelativeInMemory;
            int VABaseDebug = ImageBase + BaseOfDebugRelativeInMemory;

            writer.Place(".text", VABaseCode, BaseOfCodeRelativeOnDisk, CodeAlignment);
            writer.Place(".data", VABaseData, BaseOfDataRelativeOnDisk, DataAlignment);
            writer.Place(".idata", VABaseImport, BaseOfImportRelativeOnDisk, ImportAlignment);
            writer.Place(".rsrc", VABaseResource, BaseOfResourceRelativeOnDisk, ResourceAlignment);
            writer.Place(".debug", VABaseDebug, BaseOfDebugRelativeOnDisk, DebugAlignment);
            writer.ResolvePlaceholders(ImageBase);

            int BaseOfIATRelativeInMemory = importer.ImportAddressTableMemoryLocation - ImageBase;
            int SizeOfIAT = importer.ImportAddressTableMemorySize;

            int EntryPointRelativeInMemory = (int)entryPoint.MemoryLocation - ImageBase;

            byte[] zero = new byte[64];
            Array.Clear(zero, 0, zero.Length);

            // Default Dos header for a PE binary
            WriteArray(stream, new byte[]
                {
                    0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00,
                    0xB8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00,
                    0x0E, 0x1F, 0xBA, 0x0E, 0x00, 0xB4, 0x09, 0xCD, 0x21, 0xB8, 0x01, 0x4C, 0xCD, 0x21, 0x54, 0x68,
                    0x69, 0x73, 0x20, 0x70, 0x72, 0x6F, 0x67, 0x72, 0x61, 0x6D, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F,
                    0x74, 0x20, 0x62, 0x65, 0x20, 0x72, 0x75, 0x6E, 0x20, 0x69, 0x6E, 0x20, 0x44, 0x4F, 0x53, 0x20,
                    0x6D, 0x6F, 0x64, 0x65, 0x2E, 0x0D, 0x0D, 0x0A, 0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                });

            WriteArray(stream, new byte[]
                {
                    //pehdr
                    0x50, 0x45, 0x00, 0x00, // PE..
                    0x4c, 0x01, // x86
                });
            WriteWord(stream, NumberOfSections); // number of sections
            TimeSpan delta = DateTime.Now.Subtract(new DateTime(1970, 1, 1, 1, 0, 0));
            WriteInt(stream, (int)delta.TotalSeconds);// TimeDateStamp UNUSED
            WriteArray(stream, new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToSymbolTable UNUSED
                    0x00, 0x00, 0x00, 0x00, // NumberOfSymbols UNUSED
                    0xE0, 0x00, // SizeOfOptionalHeader
                    0xAE, 0x03, // Characteristics (executable, 32 bit, 3GB, not relocatable, debug information stripped)

                    //opthdr
                    0x0B, 0x01, // Magic (PE32)
                    0x08, 0x00, // MajorLinkerVersion UNUSED MinorLinkerVersion UNUSED
                });
            WriteInt(stream, SizeOfCodeInMemory); // SizeOfCode
            WriteInt(stream, SizeOfDataInMemory); // SizeOfInitializedData
            WriteArray(stream, new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, // SizeOfUninitializedData UNUSED
                });

            WriteInt(stream, EntryPointRelativeInMemory); // AddressOfEntryPoint
            WriteInt(stream, BaseOfCodeRelativeInMemory); // BaseOfCode
            WriteInt(stream, BaseOfDataRelativeInMemory); // BaseOfData
            WriteInt(stream, ImageBase); // ImageBase
            WriteInt(stream, SectionAlignment); // SectionAlignment
            WriteInt(stream, FileAlignment); // FileAlignment
            WriteArray(stream, new byte[]
                {
                    0x04, 0x00, 0x00, 0x00, // MajorOperatingSystemVersion UNUSED MinorOperatingSystemVersion UNUSED
                    0x00, 0x00, 0x00, 0x00, // MajorImageVersion UNUSED MinorImageVersion UNUSED
                    0x04, 0x00, 0x00, 0x00, // MajorSubsystemVersion /!\ MinorSubsystemVersion UNUSED
                    0x00, 0x00, 0x00, 0x00, // Win32VersionValue UNUSED
                });
            WriteInt(stream, SizeOfImageInMemory); // SizeOfImage
            WriteInt(stream, SizeOfHeadersOnDisk); // SizeOfHeaders
            WriteArray(stream, new byte[]
                {
                    0x00, 0x00, 0x00, 0x00, // CheckSum UNUSED
                    0x03, 0x00, // Subsystem (Windows_x86 [0x02: GUI; 0x03: Console])
                    0x00, 0x81, // DllCharacteristics UNUSED (NX_COMPAT)
                    0x00, 0x00, 0x10, 0x00, // SizeOfStackReserve UNUSED
                    0x00, 0x10, 0x00, 0x00, // SizeOfStackCommit
                    0x00, 0x00, 0x10, 0x00, // SizeOfHeapReserve
                    0x00, 0x10, 0x00, 0x00, // SizeOfHeapCommit UNUSED
                    0x00, 0x00, 0x00, 0x00, // LoaderFlags UNUSED
                    0x10, 0x00, 0x00, 0x00, // NumberOfRvaAndSizes UNUSED
               });
            //size so far 200
            Require.True(stream.Length == 248);

            // IMAGE_DIRECTORY_ENTRY_EXPORT
            WriteInt(stream, 0);
            WriteInt(stream, 0);

            // IMAGE_DIRECTORY_ENTRY_IMPORT
            WriteInt(stream, BaseOfImportRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfImport); // VirtualSize

            // IMAGE_DIRECTORY_ENTRY_RESOURCE
            WriteInt(stream, BaseOfResourceRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfResource); // VirtualSize

            for (int i = 0; i < 6; ++i)
                WriteInt(stream, 0);

            // IMAGE_DIRECTORY_ENTRY_Debug
            WriteInt(stream, BaseOfDebugRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfDebug); // VirtualSize

            for (int i = 0; i < 10; ++i)
                WriteInt(stream, 0);

            // IMAGE_DIRECTORY_ENTRY_IAT
            WriteInt(stream, BaseOfIATRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfIAT); // VirtualSize

            for (int i = 0; i < 6; ++i)
                WriteInt(stream, 0);

            Require.True(stream.Length == 376);

            byte[] textsection1 = 
                {
                    (byte)'.', (byte)'t', (byte)'e', (byte)'x', (byte)'t', 0, 0, 0, // ; Name
                };
            stream.Write(textsection1, 0, textsection1.Length);
            WriteInt(stream, SizeOfCodeInMemory); // VirtualSize
            WriteInt(stream, BaseOfCodeRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfCodeOnDisk); // SizeOfRawData
            WriteInt(stream, BaseOfCodeRelativeOnDisk); // PointerToRawData
            byte[] textsection3 = 
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToRelocations UNUSED
                    0x00, 0x00, 0x00, 0x00, // PointerToLinenumbers UNUSED
                    0x00, 0x00, // NumberOfRelocations UNUSED
                    0x00, 0x00, // NumberOfLinenumbers UNUSED
                    0x20, 0x00, 0x00, 0x60, // Characteristics (code, execute, read)
                };
            stream.Write(textsection3, 0, textsection3.Length);

            byte[] datasection1 = 
                {
                    (byte)'.', (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0, 0, 0, // ; Name
                };
            stream.Write(datasection1, 0, datasection1.Length);
            WriteInt(stream, SizeOfDataInMemory); // VirtualSize
            WriteInt(stream, BaseOfDataRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfDataOnDisk); // SizeOfRawData
            WriteInt(stream, BaseOfDataRelativeOnDisk); // PointerToRawData
            byte[] datasection3 = 
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToRelocations UNUSED
                    0x00, 0x00, 0x00, 0x00, // PointerToLinenumbers UNUSED
                    0x00, 0x00, // NumberOfRelocations UNUSED
                    0x00, 0x00, // NumberOfLinenumbers UNUSED
                    0x40, 0x00, 0x00, 0xC0, // Characteristics (initialized data, read, write)
                };
            stream.Write(datasection3, 0, datasection3.Length);

            byte[] importsection1 = 
                {
                    (byte)'.', (byte)'i', (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0, 0, // ; Name
                };
            stream.Write(importsection1, 0, datasection1.Length);
            WriteInt(stream, SizeOfImportInMemory); // VirtualSize
            WriteInt(stream, BaseOfImportRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfImportOnDisk); // SizeOfRawData
            WriteInt(stream, BaseOfImportRelativeOnDisk); // PointerToRawData
            byte[] importsection3 = 
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToRelocations UNUSED
                    0x00, 0x00, 0x00, 0x00, // PointerToLinenumbers UNUSED
                    0x00, 0x00, // NumberOfRelocations UNUSED
                    0x00, 0x00, // NumberOfLinenumbers UNUSED
                    0x40, 0x00, 0x00, 0xC0, // Characteristics (initialized data, read, write)
                };
            stream.Write(importsection3, 0, datasection3.Length);

            byte[] resourcesection1 = 
                {
                    (byte)'.', (byte)'r', (byte)'s', (byte)'r', (byte)'c', 0, 0, 0, // ; Name
                };
            stream.Write(resourcesection1, 0, resourcesection1.Length);
            WriteInt(stream, SizeOfResourceInMemory); // VirtualSize
            WriteInt(stream, BaseOfResourceRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfResourceOnDisk); // SizeOfRawData
            WriteInt(stream, BaseOfResourceRelativeOnDisk); // PointerToRawData
            byte[] resourcesection3 = 
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToRelocations UNUSED
                    0x00, 0x00, 0x00, 0x00, // PointerToLinenumbers UNUSED
                    0x00, 0x00, // NumberOfRelocations UNUSED
                    0x00, 0x00, // NumberOfLinenumbers UNUSED
                    0x40, 0x00, 0x00, 0x40, // Characteristics (initialized data, read)
                };
            stream.Write(resourcesection3, 0, resourcesection3.Length);

            byte[] debugsection1 = 
                {
                    (byte)'.', (byte)'d', (byte)'e', (byte)'b', (byte)'u', (byte)'g', 0, 0, // ; Name
                };
            stream.Write(debugsection1, 0, debugsection1.Length);
            WriteInt(stream, SizeOfDebugInMemory); // VirtualSize
            WriteInt(stream, BaseOfDebugRelativeInMemory); // VirtualAddress
            WriteInt(stream, SizeOfDebugOnDisk); // SizeOfRawData
            WriteInt(stream, BaseOfDebugRelativeOnDisk); // PointerToRawData
            byte[] debugsection3 = 
                {
                    0x00, 0x00, 0x00, 0x00, // PointerToRelocations UNUSED
                    0x00, 0x00, 0x00, 0x00, // PointerToLinenumbers UNUSED
                    0x00, 0x00, // NumberOfRelocations UNUSED
                    0x00, 0x00, // NumberOfLinenumbers UNUSED
                    0x40, 0x00, 0x00, 0x40, // Characteristics (initialized data, read)
                };
            stream.Write(debugsection3, 0, debugsection3.Length);

            Fill(stream, 0); // allign 16
            Require.True(stream.Length == SizeOfHeaders);
            Fill(stream, SizeOfHeadersOnDisk - (int)stream.Length);
            Require.True(stream.Length == SizeOfHeadersOnDisk);

            Fill(stream, BaseOfCodeRelativeOnDisk - (int)stream.Position);
            writer.WriteToStream(".text", 0x90 /*nop*/, stream, CodeAlignment);
            Fill(stream, BaseOfCodeRelativeOnDisk + SizeOfCodeOnDisk - (int)stream.Position);

            Fill(stream, BaseOfDataRelativeOnDisk - (int)stream.Position);
            writer.WriteToStream(".data", 0, stream, DataAlignment);
            Fill(stream, BaseOfDataRelativeOnDisk + SizeOfDataOnDisk - (int)stream.Position);

            Fill(stream, BaseOfImportRelativeOnDisk - (int)stream.Position);
            writer.WriteToStream(".idata", 0, stream, ImportAlignment);
            Fill(stream, BaseOfImportRelativeOnDisk + SizeOfImportOnDisk - (int)stream.Position);

            Fill(stream, BaseOfResourceRelativeOnDisk - (int)stream.Position);
            writer.WriteToStream(".rsrc", 0, stream, ResourceAlignment);
            Fill(stream, BaseOfResourceRelativeOnDisk + SizeOfResourceOnDisk - (int)stream.Position);

            Fill(stream, BaseOfDebugRelativeOnDisk - (int)stream.Position);
            writer.WriteToStream(".debug", 0, stream, DebugAlignment);
            Fill(stream, BaseOfDebugRelativeOnDisk + SizeOfDebugOnDisk - (int)stream.Position);


            // debug file

            int SizeOfGlobalPub = 16;
            int BaseOfCodeviewDataOnDisk = 0x114;
            int BaseOfModuleInCodeview = 0x48;

            byte[] moduleNameArray = (new UTF8Encoding()).GetBytes(moduleName);

            int ModuleNameLength = moduleNameArray.Length + 1;
            while ((ModuleNameLength % 4) != 0)
                ModuleNameLength += 1;

            int SizeOfModuleInCodeview = 8 + (NumberOfSections * 12) + ModuleNameLength;

            int BaseOfSrcModuleInCodeview = BaseOfModuleInCodeview + SizeOfModuleInCodeview;
            int SizeOfSrcModule = locations.Length;

            int BaseOfGlobalPubInCodeview = BaseOfSrcModuleInCodeview + SizeOfSrcModule;
            int BaseOfSymbolsInCodeview = BaseOfGlobalPubInCodeview + SizeOfGlobalPub;

            int SizeOfSegmentInCodeview = 4 + (20 * NumberOfSections);
            int SizeOfSymbols = symbols.Length;

            int SizeOfCodeviewDataOnDisk = BaseOfModuleInCodeview + SizeOfModuleInCodeview + SizeOfSymbols + SizeOfGlobalPub + SizeOfSrcModule + SizeOfSegmentInCodeview;
            int BaseOfSegmentInCodeview = BaseOfSymbolsInCodeview + SizeOfSymbols;

            WriteArray(debugStream, new byte[] 
                {
                    0x44, 0x49, // DI..
                    0x00, 0x00, // Flags
                    0x4c, 0x01, // x86
                    0x23, 0x03, // Characteristics (executable, 32 bit, 3GB, not relocatable, debug information stripped)
                });
            WriteInt(debugStream, (int)delta.TotalSeconds);// TimeDateStamp UNUSED
            WriteArray(debugStream, new byte[] 
                {
                    0x00, 0x00, 0x00, 0x00, // CheckSum UNUSED
                });
            WriteInt(debugStream, ImageBase); // ImageBase
            WriteInt(debugStream, SizeOfImageInMemory); // SizeOfImage
            WriteInt(debugStream, NumberOfSections); // NumberOfSections
            WriteInt(debugStream, 0); //ExportedNameSize
            WriteInt(debugStream, 28); // DebugDirectorySize
            WriteInt(debugStream, SectionAlignment); // Sectionalignment
            WriteInt(debugStream, BaseOfSegmentInCodeview); // ??
            WriteInt(debugStream, SizeOfSegmentInCodeview); // ??

            debugStream.Write(textsection1, 0, textsection1.Length);
            WriteInt(debugStream, SizeOfCodeInMemory); // VirtualSize
            WriteInt(debugStream, BaseOfCodeRelativeInMemory); // VirtualAddress
            WriteInt(debugStream, SizeOfCodeOnDisk); // SizeOfRawData
            WriteInt(debugStream, BaseOfCodeRelativeOnDisk); // PointerToRawData
            debugStream.Write(textsection3, 0, textsection3.Length);
            debugStream.Write(datasection1, 0, datasection1.Length);
            WriteInt(debugStream, SizeOfDataInMemory); // VirtualSize
            WriteInt(debugStream, BaseOfDataRelativeInMemory); // VirtualAddress
            WriteInt(debugStream, SizeOfDataOnDisk); // SizeOfRawData
            WriteInt(debugStream, BaseOfDataRelativeOnDisk); // PointerToRawData
            debugStream.Write(datasection3, 0, datasection3.Length);
            debugStream.Write(importsection1, 0, datasection1.Length);
            WriteInt(debugStream, SizeOfImportInMemory); // VirtualSize
            WriteInt(debugStream, BaseOfImportRelativeInMemory); // VirtualAddress
            WriteInt(debugStream, SizeOfImportOnDisk); // SizeOfRawData
            WriteInt(debugStream, BaseOfImportRelativeOnDisk); // PointerToRawData
            debugStream.Write(importsection3, 0, datasection3.Length);
            debugStream.Write(resourcesection1, 0, datasection1.Length);
            WriteInt(debugStream, SizeOfResourceInMemory); // VirtualSize
            WriteInt(debugStream, BaseOfResourceRelativeInMemory); // VirtualAddress
            WriteInt(debugStream, SizeOfResourceOnDisk); // SizeOfRawData
            WriteInt(debugStream, BaseOfResourceRelativeOnDisk); // PointerToRawData
            debugStream.Write(resourcesection3, 0, datasection3.Length);
            debugStream.Write(debugsection1, 0, datasection1.Length);
            WriteInt(debugStream, SizeOfDebugInMemory); // VirtualSize
            WriteInt(debugStream, BaseOfDebugRelativeInMemory); // VirtualAddress
            WriteInt(debugStream, SizeOfDebugOnDisk); // SizeOfRawData
            WriteInt(debugStream, BaseOfDebugRelativeOnDisk); // PointerToRawData
            debugStream.Write(debugsection3, 0, datasection3.Length);

            Require.True(NumberOfSections == 5);

            //IMAGE_DEBUG_DIRECTORY
            WriteInt(debugStream, 0);  // Characteristics
            WriteInt(debugStream, (int)delta.TotalSeconds);// TimeDateStamp UNUSED
            WriteWord(debugStream, 0); // MajorVersion
            WriteWord(debugStream, 0); // MinorVersion
            WriteInt(debugStream, 2); // IMAGE_DEBUG_TYPE_CODEVIEW
            WriteInt(debugStream, SizeOfCodeviewDataOnDisk);
            WriteInt(debugStream, 0);
            WriteInt(debugStream, BaseOfCodeviewDataOnDisk);

            // http://www.openwatcom.org/ftp/devel/docs/CodeView.pdf 7.3

            Require.True(BaseOfCodeviewDataOnDisk == debugStream.Position);

            WriteArray(debugStream, new byte[] 
                {
                    (byte)'N', (byte)'B', (byte)'0', (byte)'9'
                });
            WriteInt(debugStream, 8); // lfaBase 
            //SubsectionDirectory
            WriteWord(debugStream, 16); // cbDirHeader
            WriteWord(debugStream, 12); // cdDirEntry
            WriteInt(debugStream, 3); // cDir
            WriteInt(debugStream, 0); // lfoNextDir UNUSED
            WriteInt(debugStream, 0); // Flags UNUSED

            //SubsectionEntry
            // points to a module definition
            // offset is static because we know where to put it
            WriteWord(debugStream, 0x120); // sstModule
            WriteWord(debugStream, 1); // iMod
            WriteInt(debugStream, BaseOfModuleInCodeview); // lfo
            WriteInt(debugStream, SizeOfModuleInCodeview); // szSstModule

            //SubsectionEntry
            WriteWord(debugStream, 0x127); // sstSrcModule
            WriteWord(debugStream, 1); // iMod
            WriteInt(debugStream, BaseOfSrcModuleInCodeview); // lfo
            WriteInt(debugStream, SizeOfSrcModule);

            //SubsectionEntry
            WriteWord(debugStream, 0x12A); // sstGlobalPub
            WriteWord(debugStream, 0xffff); // iMod
            WriteInt(debugStream, BaseOfGlobalPubInCodeview); // lfo
            WriteInt(debugStream, SizeOfGlobalPub + SizeOfSymbols);

            //SubsectionEntry
            // SegmentMap
            WriteWord(debugStream, 0x12D); // sstSegMap
            WriteWord(debugStream, 0xFFFF); // iMod
            WriteInt(debugStream, BaseOfSegmentInCodeview); // lfo
            WriteInt(debugStream, SizeOfSegmentInCodeview); // szSstSegMap

            Require.True(debugStream.Position == BaseOfModuleInCodeview + BaseOfCodeviewDataOnDisk);
            // WriteSstModule
            WriteWord(debugStream, 0); // ovlNumber
            WriteWord(debugStream, 0); // iLib
            WriteWord(debugStream, NumberOfSections); // cSeg
            debugStream.WriteByte((byte)'C');
            debugStream.WriteByte((byte)'V'); // Style

            WriteWord(debugStream, 1); // Seg
            WriteWord(debugStream, 0); // pad
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfCodeInMemory); // cbSeg

            WriteWord(debugStream, 2); // Seg
            WriteWord(debugStream, 0); // pad
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfDataInMemory); // cbSeg

            WriteWord(debugStream, 3); // Seg
            WriteWord(debugStream, 0); // pad
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfImportInMemory); // cbSeg

            WriteWord(debugStream, 4); // Seg
            WriteWord(debugStream, 0); // pad
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfResourceInMemory); // cbSeg

            WriteWord(debugStream, 5); // Seg
            WriteWord(debugStream, 0); // pad
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfDebugInMemory); // cbSeg

            Require.True(NumberOfSections == 5);

            debugStream.WriteByte((byte)moduleNameArray.Length);
            debugStream.Write(moduleNameArray, 0, moduleNameArray.Length);
            Fill(debugStream, ModuleNameLength - (moduleNameArray.Length + 1));

            Require.True(debugStream.Position == BaseOfModuleInCodeview + BaseOfCodeviewDataOnDisk + SizeOfModuleInCodeview);

            Require.True(debugStream.Position == BaseOfSrcModuleInCodeview + BaseOfCodeviewDataOnDisk);

            //WriteSrcLocations sstSrcModule
            locations.WriteToStream(debugStream);

            Require.True(debugStream.Position == BaseOfSrcModuleInCodeview + BaseOfCodeviewDataOnDisk + SizeOfSrcModule);

            Require.True(debugStream.Position == BaseOfGlobalPubInCodeview + BaseOfCodeviewDataOnDisk);

            //WriteGlabalPub
            WriteWord(debugStream, 0); // symHash
            WriteWord(debugStream, 0); // addrHash
            WriteInt(debugStream, SizeOfSymbols); // cbSymbol
            WriteInt(debugStream, 0); // cbSymHash
            WriteInt(debugStream, 0); /// cbAddrHash

            Require.True(debugStream.Position == BaseOfGlobalPubInCodeview + BaseOfCodeviewDataOnDisk + SizeOfGlobalPub);

            Require.True(debugStream.Position == BaseOfSymbolsInCodeview + BaseOfCodeviewDataOnDisk);

            symbols.WriteToStream(debugStream);

            Require.True(debugStream.Position == BaseOfSymbolsInCodeview + BaseOfCodeviewDataOnDisk + SizeOfSymbols);

            // WriteSstSegMap
            Require.True(debugStream.Position == BaseOfSegmentInCodeview + BaseOfCodeviewDataOnDisk);

            //pagina 82
            WriteWord(debugStream, NumberOfSections); // cSeg
            WriteWord(debugStream, NumberOfSections); // cSegLog

            WriteWord(debugStream, 0); // flags
            WriteWord(debugStream, 0); // ovl
            WriteWord(debugStream, 0); // group
            WriteWord(debugStream, 1); // frame (I); // ??
            WriteWord(debugStream, 0xFFFF); // iSegName
            WriteWord(debugStream, 0xFFFF); // iClassName
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfCodeInMemory);

            WriteWord(debugStream, 0); // flags
            WriteWord(debugStream, 0); // ovl
            WriteWord(debugStream, 0); // group
            WriteWord(debugStream, 2); // frame (I);
            WriteWord(debugStream, 0xFFFF); // iSegName
            WriteWord(debugStream, 0xFFFF); // iClassName
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfDataInMemory);

            WriteWord(debugStream, 0); // flags
            WriteWord(debugStream, 0); // ovl
            WriteWord(debugStream, 0); // group
            WriteWord(debugStream, 3); // frame (I);
            WriteWord(debugStream, 0xFFFF); // iSegName
            WriteWord(debugStream, 0xFFFF); // iClassName
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfImportInMemory);

            WriteWord(debugStream, 0); // flags
            WriteWord(debugStream, 0); // ovl
            WriteWord(debugStream, 0); // group
            WriteWord(debugStream, 4); // frame (I);
            WriteWord(debugStream, 0xFFFF); // iSegName
            WriteWord(debugStream, 0xFFFF); // iClassName
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfResourceInMemory);

            WriteWord(debugStream, 0); // flags
            WriteWord(debugStream, 0); // ovl
            WriteWord(debugStream, 0); // group
            WriteWord(debugStream, 5); // frame (I);
            WriteWord(debugStream, 0xFFFF); // iSegName
            WriteWord(debugStream, 0xFFFF); // iClassName
            WriteInt(debugStream, 0); // offset
            WriteInt(debugStream, SizeOfDebugInMemory);

            Require.True(NumberOfSections == 5);

            Require.True(debugStream.Position == BaseOfSegmentInCodeview + BaseOfCodeviewDataOnDisk + SizeOfSegmentInCodeview);

            Require.True(SizeOfCodeviewDataOnDisk == debugStream.Position - BaseOfCodeviewDataOnDisk);
        }

        public void SaveToFile()
        {
            FileStream f = new FileStream(moduleName + ".exe", FileMode.Create);
            stream.WriteTo(f);
            f.Close();
            f = new FileStream(moduleName + ".dbg", FileMode.Create);
            debugStream.WriteTo(f);
            f.Close();
        }

        private static void Fill(Stream stream, int count)
        {
            for (int i = 0; i < count; ++i)
                stream.WriteByte(0);
        }

        private static void WriteArray(Stream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }

        private static void WriteInt(Stream stream, int value)
        {
            unchecked
            {
                stream.WriteByte((byte)((value >> 0) & 0xff));
                stream.WriteByte((byte)((value >> 8) & 0xff));
                stream.WriteByte((byte)((value >> 16) & 0xff));
                stream.WriteByte((byte)((value >> 24) & 0xff));
            }
        }

        private static void WriteWord(Stream stream, int value)
        {
            unchecked
            {
                stream.WriteByte((byte)((value >> 0) & 0xff));
                stream.WriteByte((byte)((value >> 8) & 0xff));
            }
        }

        private static int Align(int value, int alignment)
        {
            if ((value % alignment) == 0)
                return value;
            else
                return value + alignment - (value % alignment);
        }

        private void WriteDebugRedirect()
        {
            Region r = writer.AllocateRegion(".debug");
            r.WriteInt32(0); // Characteristics
            r.WriteInt32(0); // Timestamp
            r.WriteInt16(0); // Major version of debug format
            r.WriteInt16(0); // Minor
            r.WriteInt32(4); // type: IMAGE_DEBUG_TYPE_MISC
            IntToken t = r.InsertIntToken(); // SizeOfData;
            Placeholder startLocation = r.CurrentLocation.Increment(8);
            r.WritePlaceholderRelative(startLocation); //starts immediatly after this header
            r.WritePlaceholderFile(startLocation); //starts immediatly after this header


            // /!\ windbg doesn't seem to allow unicode names form dbg files
            string dbgName = moduleName + ".dbg";
            int len = dbgName.Length + 1;
            int fill = 4 - (len % 4);
            if (fill == 4)
                fill = 0;
            len += fill;

            r.WriteInt32(1); // DataType: IMAGE_DEBUG_MISC_EXENAME
            r.WriteInt32(len);
            r.WriteByte(0); // Ascii
            r.Write(new byte[] { 0, 0, 0 }); // reserved
            r.WriteAsUtf8NullTerminated(dbgName);
            for (int i = 0; i < fill; ++i)
                r.WriteByte(0);
            t.SetValue((int)(r.CurrentLocation.MemoryDistanceFrom(startLocation)));
        }

        private void WriteVersionHeader()
        {
            Region resourceHeader = writer.AllocateRegion(".rsrc");

            // Resouce Directory Table
            resourceHeader.WriteInt32(0); //Characteristics
            resourceHeader.WriteInt32(0);// TimeDateStamp
            resourceHeader.WriteInt16(0); // mayor version
            resourceHeader.WriteInt16(0); // minor verson
            resourceHeader.WriteInt16(0); // number of named entries
            resourceHeader.WriteInt16(1); // number of id'ed entries
            // Resource Directory Entry
            resourceHeader.WriteInt32(0x10); // ID (VERSION)
            resourceHeader.WriteInt32(0x80000018); //pointer to next directory
            // Resouce Directory Table
            resourceHeader.WriteInt32(0); //Characteristics
            resourceHeader.WriteInt32(0);// TimeDateStamp
            resourceHeader.WriteInt16(0); // mayor version
            resourceHeader.WriteInt16(0); // minor verson
            resourceHeader.WriteInt16(0); // number of named entries
            resourceHeader.WriteInt16(1); // number of id'ed entries
            // Resource Directory Entry
            resourceHeader.WriteInt32(1); // ID (1)
            resourceHeader.WriteInt32(0x80000030); //pointer to next directory
            // Resouce Directory Table
            resourceHeader.WriteInt32(0); //Characteristics
            resourceHeader.WriteInt32(0);// TimeDateStamp
            resourceHeader.WriteInt16(0); // mayor version
            resourceHeader.WriteInt16(0); // minor verson
            resourceHeader.WriteInt16(0); // number of named entries
            resourceHeader.WriteInt16(1); // number of id'ed entries
            // Resource Directory Entry
            resourceHeader.WriteInt32(0); // ID 
            resourceHeader.WriteInt32(0x48); //pointer to leaf
            // Resource Data Entry
            resourceHeader.WritePlaceholderRelative(resourceHeader.CurrentLocation.Increment(0x10));
            IntToken SizeOfResourceBody1 = resourceHeader.InsertIntToken();
            resourceHeader.WriteInt32(0); // codepage (default unicode)
            resourceHeader.WriteInt32(0); // reserved

            long RelativeSizeOfHeader = resourceHeader.CurrentLocation.Offset;

            // struct VS_VERSIONINFO { 
            WordToken SizeOfResourceBody2 = resourceHeader.InsertWordToken(); // wLength
            resourceHeader.WriteInt16(0x34); // wValueLength
            resourceHeader.WriteInt16(0); // wType
            resourceHeader.WriteAsUtf16NullTerminated2("VS_VERSION_INFO");
            resourceHeader.WriteInt16(0); // padding1

            // struct VS_FIXEDFILEINFO { 
            resourceHeader.WriteInt32(0xFEEF04BD); // dwSignature
            resourceHeader.WriteInt32(0x00010000); // dwStrucVersion
            resourceHeader.WriteInt32(0x00010000); // dwFileVersionMS
            resourceHeader.WriteInt32(0x00000000); // dwFileVersionLS
            resourceHeader.WriteInt32(0x00010000); // dwProductVersionMS
            resourceHeader.WriteInt32(0x00000000); // dwProductVersionLS

            resourceHeader.WriteInt32(0x3f); // dwFileFlagsMask; 
            resourceHeader.WriteInt32(0); // dwFileFlags; 
            resourceHeader.WriteInt32(4); // dwFileOS; 
            resourceHeader.WriteInt32(1); // dwFileType
            resourceHeader.WriteInt32(0); // dwFileSubtype
            resourceHeader.WriteInt32(0); // dwFileDateMS
            resourceHeader.WriteInt32(0); // dwFileDateLS

            // rest of  //struct VS_VERSIONINFO {
            //  WORD  Padding2[]; 
            //WORD  Children[]; 

            WordToken varFileInfo = WriteEntry(resourceHeader, 0, 1, "VarFileInfo");
            WordToken translation = WriteEntry(resourceHeader, 4, 0, "Translation");
            resourceHeader.WriteInt32(0x04B00000); // codepage unicode
            translation.SetDistanceSinceTaken();
            varFileInfo.SetDistanceSinceTaken();
            WordToken varStringFileInfo = WriteEntry(resourceHeader, 0, 1, "StringFileInfo");
            WordToken varVersion = WriteEntry(resourceHeader, 0, 1, "000004b0");
            WriteEntry2(resourceHeader, "FileDescription", "Something compiled by Bart's compiler.");
            WriteEntry2(resourceHeader, "FileVersion", "1.0.0.0");
            WriteEntry2(resourceHeader, "InternalName", moduleName + ".exe");
            WriteEntry2(resourceHeader, "LegalCopyright", " ");
            WriteEntry2(resourceHeader, "OriginalFilename", moduleName + ".exe");
            WriteEntry2(resourceHeader, "ProductName", moduleName);
            WriteEntry2(resourceHeader, "ProductVersion", "1.0.0.0");
            varVersion.SetDistanceSinceTaken();
            varStringFileInfo.SetDistanceSinceTaken();

            RelativeSizeOfHeader = resourceHeader.CurrentLocation.Offset - RelativeSizeOfHeader;
            SizeOfResourceBody1.SetValue((int)RelativeSizeOfHeader);
            SizeOfResourceBody2.SetValue((int)RelativeSizeOfHeader);
        }

        private static WordToken WriteEntry(Region resourceHeader, int valueLength, int type, string value)
        {
            WordToken result = resourceHeader.InsertWordToken(); // wLength
            resourceHeader.WriteInt16((short)valueLength); // w
            resourceHeader.WriteInt16((short)type); // type
            resourceHeader.WriteAsUtf16NullTerminated2(value);
            //padding
            if ((resourceHeader.CurrentLocation.Offset % 4) != 0)
                resourceHeader.WriteInt16(0);
            return result;
        }

        private static void WriteEntry2(Region resourceHeader, string type, string data)
        {
            WordToken length = resourceHeader.InsertWordToken();
            resourceHeader.WriteInt16((short)(data.Length + 1)); // w
            resourceHeader.WriteInt16(1); // type
            resourceHeader.WriteAsUtf16NullTerminated2(type);
            //padding
            if ((resourceHeader.CurrentLocation.Offset % 4) != 0)
                resourceHeader.WriteInt16(0);
            resourceHeader.WriteAsUtf16NullTerminated2(data);
            //padding
            if ((resourceHeader.CurrentLocation.Offset % 4) != 0)
                resourceHeader.WriteInt16(0);
            length.SetDistanceSinceTaken();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (stream != null)
                {
                    stream.Dispose();
                    stream = null;
                }
                if (debugStream != null)
                {
                    debugStream.Dispose();
                    debugStream = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
