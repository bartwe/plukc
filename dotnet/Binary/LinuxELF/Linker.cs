using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.AccessControl;
using Compiler.Binary.LinuxELF;

namespace Compiler.Binary.LinuxELF
{
    public class Linker : IDisposable
    {
        bool is64bit;
        long imageBase;
        Sections sections;
        string moduleName;
        Region output;
        Region entryPoint;
        StringTable stringTable;
        long phdrma;
        long phdrsz;
        long sectionheadersize;

        Section codeSection;
        Section dataSection;
        Section rodataSection;
        Section interpSection;
        Section strtabSection;
        Section symtabSection;
        Section hashSection;

        Section dynsymSection;
        Section dynstrSection;
        Section dynamicSection;
        Section pltSection;
        Section gotpltSection;
        Section relapltSection;

        Section debugInfoSection;
        Section debugAbbrevSection;
        Section debugLineSection;

        NumberToken programHeaderTable_fo;
        NumberToken sectionHeaderTable_fo;

        //        long execLength;
        long readWriteLength;
        //        long readonlyLength;
        long fileImageLength;

        short ProgramHeaderTableEntrySize;
        short ProgramHeaderEntryCount = 5;
        short SectionHeaderEntrySize;
        short SectionHeaderEntryCount = 1 + 16;
        const int StringHeaderSectionIndex = 1;

        long SizeOfElfHeader;

        const long MaxPageSize = 2 * 1024 * 1024;

        public Linker(string moduleName, Sections sections, StringTable stringTable, bool is64bit)
        {
            this.is64bit = is64bit;
            if (is64bit)
            {
                SizeOfElfHeader = 64;
                ProgramHeaderTableEntrySize = 56;
                SectionHeaderEntrySize = 64;
            }
            else
            {
                SizeOfElfHeader = 52;
                ProgramHeaderTableEntrySize = 32;
                SectionHeaderEntrySize = 40;
            }
            Require.NotEmpty(moduleName);
            Require.Assigned(sections);
            Require.Assigned(stringTable);
            this.stringTable = stringTable;
            this.moduleName = moduleName;
            this.sections = sections;
            imageBase = 0x1000000;
            SetupInterp();
            output = new Compiler.Region(-1, is64bit);
            phdrsz = ProgramHeaderEntryCount * ProgramHeaderTableEntrySize;
            sectionheadersize = SectionHeaderEntryCount * SectionHeaderEntrySize;

        }

        public static void RegisterSections(Sections sections)
        {
            sections.RegisterSection(".strtab", 1);
            sections.RegisterSection(".text", 2);
            sections.RegisterSection(".data", 3);
            sections.RegisterSection(".interp", 4);
            sections.RegisterSection(".rodata", 5);
            sections.RegisterSection(".symtab", 6);
            sections.RegisterSection(".hash", 7);
            sections.RegisterSection(".dynsym", 8);
            sections.RegisterSection(".dynstr", 9);
            sections.RegisterSection(".dynamic", 10);
            sections.RegisterSection(".plt", 11);
            sections.RegisterSection(".got.plt", 12);
            sections.RegisterSection(".rela.plt", 13);
            sections.RegisterSection(".debug_info", 14);
            sections.RegisterSection(".debug_abbrev", 15);
            sections.RegisterSection(".debug_line", 16);
        }

        /// <param name="entryPoint">
        /// Region that contains the entrypoint, use its MemoryAddress after it has been placed.
        /// </param>
        public void SetEntryPoint(Region entryPoint)
        {
            Require.Assigned(entryPoint);
            this.entryPoint = entryPoint;
        }

        public void SaveToFile()
        {
            codeSection = sections.GetSection(".text");
            dataSection = sections.GetSection(".data");
            interpSection = sections.GetSection(".interp");
            strtabSection = sections.GetSection(".strtab");
            rodataSection = sections.GetSection(".rodata");
            symtabSection = sections.GetSection(".symtab");
            hashSection = sections.GetSection(".hash");
            dynsymSection = sections.GetSection(".dynsym");
            dynstrSection = sections.GetSection(".dynstr");
            dynamicSection = sections.GetSection(".dynamic");
            pltSection = sections.GetSection(".plt");
            gotpltSection = sections.GetSection(".got.plt");
            relapltSection = sections.GetSection(".rela.plt");
            debugInfoSection = sections.GetSection(".debug_info");
            debugAbbrevSection = sections.GetSection(".debug_abbrev");
            debugLineSection = sections.GetSection(".debug_line");

            stringTable.WriteTo(strtabSection.AllocateRegion());

            long totalSize = SizeOfElfHeader;

            // readonly
            FakePlace(interpSection, ref totalSize);
            FakePlace(strtabSection, ref totalSize);
            FakePlace(rodataSection, ref totalSize);
            FakePlace(symtabSection, ref totalSize);
            FakePlace(hashSection, ref totalSize);
            FakePlace(dynsymSection, ref totalSize);
            FakePlace(dynstrSection, ref totalSize);
            FakePlace(relapltSection, ref totalSize);
            FakePlace(debugInfoSection, ref totalSize);
            FakePlace(debugAbbrevSection, ref totalSize);
            FakePlace(debugLineSection, ref totalSize);
            // executable
            FakePlace(codeSection, ref totalSize);
            FakePlace(pltSection, ref totalSize);

            // readwrite
            FakePlace(dataSection, ref totalSize);
            FakePlace(gotpltSection, ref totalSize);
            FakePlace(dynamicSection, ref totalSize);
            //phdr
            if ((totalSize % 16) != 0)
                totalSize += 16 - (totalSize % 16);
            totalSize += phdrsz;

            // section
            if ((totalSize % 16) != 0)
                totalSize += 16 - (totalSize % 16);
            totalSize += sectionheadersize;




            long address = imageBase;
            address += SizeOfElfHeader;

            // readonly
            Place(interpSection, ref address);
            Place(strtabSection, ref address);
            Place(rodataSection, ref address);
            Place(symtabSection, ref address);
            Place(hashSection, ref address);
            Place(dynsymSection, ref address);
            Place(dynstrSection, ref address);
            Place(relapltSection, ref address);
            Place(debugInfoSection, ref address);
            Place(debugAbbrevSection, ref address);
            Place(debugLineSection, ref address);

            //            readonlyLength = address - interpSection.MemoryAddress;
            /*
            long x = totalSize;
            while (x >= MaxPageSize)
            {
                address += MaxPageSize;
                x -= MaxPageSize;
            }
            address += MaxPageSize;
*/
            // executable
            Place(codeSection, ref address);
            Place(pltSection, ref address);

            //            execLength = address - codeSection.MemoryAddress;
            address += MaxPageSize;

            // readwrite
            Place(dataSection, ref address);
            Place(gotpltSection, ref address);
            Place(dynamicSection, ref address);

            readWriteLength = address - dataSection.MemoryAddress;
            address += MaxPageSize;

            //phdr
            if ((address % 16) != 0)
                address += 16 - (address % 16);
            phdrma = address - 2 * MaxPageSize;
            address += phdrsz;

            // section
            if ((address % 16) != 0)
                address += 16 - (address % 16);
            address += sectionheadersize;

            fileImageLength = address - imageBase - 2 * MaxPageSize;
            Require.True(totalSize == fileImageLength);

            //TODO: i don't know why i placed this limit here, it seems to work fine without....
            //            Require.True(fileImageLength < MaxPageSize);

            sections.ResolvePlaceholders(imageBase);

            WriteElfHeader();
            WriteBody();
            using (FileStream f = new FileStream(moduleName, FileMode.Create))
                output.WriteToStream(f);
#if MONO   
            Mono.Unix.Native.Syscall.chmod(moduleName, (Mono.Unix.Native.FilePermissions)493); //0755
#endif
        }


        private static void Place(Section section, ref long address)
        {
            address = section.AlignMemoryAddress(address);
            address = section.Place(address);
        }

        private static void FakePlace(Section section, ref long address)
        {
            address = section.AlignMemoryAddress(address);
            address = section.FakePlace(address);
        }

        private void SetupInterp()
        {
            Section interp = sections.GetSection(".interp");
            if (is64bit)
                interp.AllocateRegion().WriteAsUtf8NullTerminated("/lib64/ld-linux-x86-64.so.2");
            else
                interp.AllocateRegion().WriteAsUtf8NullTerminated("/lib/ld-linux.so.2");
        }


        private void WriteElfHeader()
        {
            //e_ident
            // EI_MAG0 to EI_MAG3
            output.Write(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' });
            // EI_CLASS
            if (is64bit)
                output.WriteByte(2); // ELFCLASS64
            else
                output.WriteByte(1); // ELFCLASS32
            // EI_DATA
            output.WriteByte(1); // ELFDATA2LSB
            // EI_VERSION
            output.WriteByte(1); // EV_CURRENT
            // EI_PAD
            output.Write(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 });

            Require.Equals(16, output.Length);

            //e_type
            output.WriteInt16(2); //ET_EXEC
            //e_machine
            if (is64bit)
                output.WriteInt16(62); //EM_X86_64
            else
                output.WriteInt16(3); // EM_386
            //e_version
            output.WriteInt32(1); // EV_CURRENT
            //e_entry
            output.WriteNumber(entryPoint.MemoryLocation);

            if (is64bit)
                Require.Equals(32, output.Length);
            else
                Require.Equals(28, output.Length);

            //e_phoff
            programHeaderTable_fo = output.InsertNumberToken();
            //e_shoff
            sectionHeaderTable_fo = output.InsertNumberToken();

            if (is64bit)
                Require.Equals(48, output.Length);
            else
                Require.Equals(36, output.Length);

            //e_flags
            output.WriteInt32(0);
            //e_ehsize
            WordToken elfHeaderSize = output.InsertWordToken();

            //e_phentsize
            output.WriteInt16(ProgramHeaderTableEntrySize);
            //e_phnum
            output.WriteInt16(ProgramHeaderEntryCount);
            //e_shentsize
            output.WriteInt16(SectionHeaderEntrySize);
            //e_shnum
            output.WriteInt16(SectionHeaderEntryCount);
            // e_shstrndx
            output.WriteInt16(StringHeaderSectionIndex);

            elfHeaderSize.SetValue(output.Length);
            Require.Equals(SizeOfElfHeader, output.Length);
        }

        private void WriteBody()
        {
            // readonly
            WriteSection(interpSection, 0);
            WriteSection(strtabSection, 0);
            WriteSection(rodataSection, 0);
            WriteSection(symtabSection, 0);
            WriteSection(hashSection, 0);
            WriteSection(dynsymSection, 0);
            WriteSection(dynstrSection, 0);
            WriteSection(relapltSection, 0);
            WriteSection(debugInfoSection, 0);
            WriteSection(debugAbbrevSection, 0);
            WriteSection(debugLineSection, 0);

            // executable
            WriteSection(codeSection, 0x90); // fill with nops
            WriteSection(pltSection, 0x90); // fill with nops

            // readwrite
            WriteSection(dataSection, 0);
            WriteSection(gotpltSection, 0);
            WriteSection(dynamicSection, 0);

            WriteProgramHeaders();
            WriteSectionHeaders();
        }

        private void WriteSection(Section section, byte fill)
        {
            section.AlignFileOffset(output);
            section.FileOffset = output.Length;
            section.WriteToRegion(output, fill);
            Require.True(section.Length == output.Length - section.FileOffset);
        }

        private void WriteProgramHeaders()
        {
            output.Align(16, 0);
            programHeaderTable_fo.SetValue(output.Length);

            long phdrfo = output.Length;
            //phdr
            WriteProgramHeader(6, 4, phdrfo, phdrma, phdrsz, 16);
            //interp
            WriteProgramHeader(3, 4, interpSection.FileOffset, interpSection.MemoryAddress, interpSection.Length, interpSection.RegionAlignment);

            //readonly
            //            WriteProgramHeader(1, 4, interpSection.FileOffset, interpSection.MemoryAddress, readonlyLength, MaxPageSize);
            //fileimage (readonly + executable)
            WriteProgramHeader(1, 5, 0, imageBase, fileImageLength, MaxPageSize);
            //            //executable
            //            WriteProgramHeader(1, 5, codeSection.FileOffset, codeSection.MemoryAddress, execLength, MaxPageSize);
            //readwrite
            WriteProgramHeader(1, 6, dataSection.FileOffset, dataSection.MemoryAddress, readWriteLength, MaxPageSize);

            //dynamic
            WriteProgramHeader(2, 6, dynamicSection.FileOffset, dynamicSection.MemoryAddress, dynamicSection.Length, dynamicSection.RegionAlignment);
        }

        private void WriteProgramHeader(int kind, int access, long disk, long memory, long length, long allignment)
        {
            Require.True((disk % allignment) == (memory % allignment));
            Require.True((disk % MaxPageSize) == (memory % MaxPageSize));
            output.WriteInt32(kind);
            if (is64bit)
                output.WriteInt32(access);
            output.WriteNumber(disk);
            output.WriteNumber(memory);
            output.WriteNumber(memory);
            output.WriteNumber(length);
            output.WriteNumber(length);
            if (!is64bit)
                output.WriteInt32(access);
            output.WriteNumber(allignment);
        }

        private void WriteSectionHeaders()
        {
            output.Align(16, 0);
            sectionHeaderTable_fo.SetValue(output.Length);
            WriteSectionHeader("", 0, 0, 0, 0, 0, 0, 0, 0, 0);
            WriteSectionHeader(strtabSection.Name, 3, 2, strtabSection.MemoryAddress, strtabSection.FileOffset, strtabSection.Length, 0, 0, strtabSection.RegionAlignment, 0);
            WriteSectionHeader(codeSection.Name, 1, 6, codeSection.MemoryAddress, codeSection.FileOffset, codeSection.Length, 0, 0, codeSection.RegionAlignment, 0);
            WriteSectionHeader(dataSection.Name, 1, 3, dataSection.MemoryAddress, dataSection.FileOffset, dataSection.Length, 0, 0, dataSection.RegionAlignment, 0);
            WriteSectionHeader(interpSection.Name, 1, 2, interpSection.MemoryAddress, interpSection.FileOffset, interpSection.Length, 0, 0, interpSection.RegionAlignment, 0);
            WriteSectionHeader(rodataSection.Name, 1, 2, rodataSection.MemoryAddress, rodataSection.FileOffset, rodataSection.Length, 0, 0, rodataSection.RegionAlignment, 0);
            WriteSectionHeader(symtabSection.Name, 2, 2, symtabSection.MemoryAddress, symtabSection.FileOffset, symtabSection.Length, 1, 1, symtabSection.RegionAlignment, is64bit ? 24 : 16);

            WriteSectionHeader(hashSection.Name, 5, 2, hashSection.MemoryAddress, hashSection.FileOffset, hashSection.Length, 8, 0, hashSection.RegionAlignment, 4);
            WriteSectionHeader(dynsymSection.Name, 11, 2, dynsymSection.MemoryAddress, dynsymSection.FileOffset, dynsymSection.Length, 9, 1, dynsymSection.RegionAlignment, is64bit ? 24 : 16);
            WriteSectionHeader(dynstrSection.Name, 3, 2, dynstrSection.MemoryAddress, dynstrSection.FileOffset, dynstrSection.Length, 0, 0, dynstrSection.RegionAlignment, 0);
            WriteSectionHeader(dynamicSection.Name, 6, 3, dynamicSection.MemoryAddress, dynamicSection.FileOffset, dynamicSection.Length, 9, 0, dynamicSection.RegionAlignment, is64bit ? 16 : 8);
            WriteSectionHeader(pltSection.Name, 1, 6, pltSection.MemoryAddress, pltSection.FileOffset, pltSection.Length, 0, 0, pltSection.RegionAlignment, is64bit ? 16 : 8);
            WriteSectionHeader(gotpltSection.Name, 1, 3, gotpltSection.MemoryAddress, gotpltSection.FileOffset, gotpltSection.Length, 0, 0, gotpltSection.RegionAlignment, is64bit ? 8 : 4);
            WriteSectionHeader(relapltSection.Name, 4, 2, relapltSection.MemoryAddress, relapltSection.FileOffset, relapltSection.Length, 8, 12, relapltSection.RegionAlignment, is64bit ? 24 : 12);
            WriteSectionHeader(debugInfoSection.Name, 1, 0, 0, debugInfoSection.FileOffset, debugInfoSection.Length, 0, 0, debugInfoSection.RegionAlignment, 0);
            WriteSectionHeader(debugAbbrevSection.Name, 1, 0, 0, debugAbbrevSection.FileOffset, debugAbbrevSection.Length, 0, 0, debugAbbrevSection.RegionAlignment, 0);
            WriteSectionHeader(debugLineSection.Name, 1, 0, 0, debugLineSection.FileOffset, debugLineSection.Length, 0, 0, debugLineSection.RegionAlignment, 0);
        }

        private void WriteSectionHeader(string name, int type, long flags, long memoryAddress, long fileOffset, long size, int link, int info, long allignment, long entsize)
        {
            output.WriteInt32(stringTable.Get(name));
            output.WriteInt32(type);
            output.WriteNumber(flags);
            output.WriteNumber(memoryAddress);
            output.WriteNumber(fileOffset);
            output.WriteNumber(size);
            output.WriteInt32(link);
            output.WriteInt32(info);
            output.WriteNumber(allignment);
            output.WriteNumber(entsize);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                output.Dispose();
                output = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
