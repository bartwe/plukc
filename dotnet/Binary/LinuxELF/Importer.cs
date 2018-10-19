using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.LinuxELF
{
    class Importer : Compiler.Importer
    {
        Region mainRegion;
        StringTable dynstr;
        Region dynstrRegion;
        DynamicSymbols dynsym;
        Relocator relocator;
        Set<string> libraries = new Set<string>();
        Set<string> entryPoints = new Set<string>();
        Dictionary<string, Placeholder> pltEntry = new Dictionary<string, Placeholder>();
        Dictionary<string, Placeholder> pltgotEntry = new Dictionary<string, Placeholder>();
        Region pltgot;
        Region plt;
        Region relapltRegion;
        Placeholder plt0;
        NumberToken dynamicTokenSize;

        public Importer(Sections sections)
        {
            relapltRegion = sections.GetSection(".rela.plt").AllocateRegion();
            relocator = new Relocator(relapltRegion);
            mainRegion = sections.GetSection(".dynamic").AllocateRegion();
            dynstrRegion = sections.GetSection(".dynstr").AllocateRegion();
            dynstr = new StringTable();
            plt = sections.GetSection(".plt").AllocateRegion();
            pltgot = sections.GetSection(".got.plt").AllocateRegion();
            dynsym = new DynamicSymbols(sections, dynstr);

            dynamicTokenSize = dynsym.Write(mainRegion.CurrentLocation, "_DYNAMIC");

            mainRegion.WriteNumber(3); // dt_pltgot
            mainRegion.WritePlaceholder(pltgot.BaseLocation);
            mainRegion.WriteNumber(4); // dt_hash
            mainRegion.WritePlaceholder(dynsym.HashRegion.BaseLocation);
            mainRegion.WriteNumber(5); // dt_strtab
            mainRegion.WritePlaceholder(dynstrRegion.BaseLocation);
            mainRegion.WriteNumber(6); // dt_symtab
            mainRegion.WritePlaceholder(dynsym.SymbolRegion.BaseLocation);
            mainRegion.WriteNumber(11); // dt_syment
            mainRegion.WriteNumber(dynsym.ElementSize);
            //            mainRegion.WriteNumber(15); // dt_rpath
            //            mainRegion.WriteNumber(dynstr.Get(".:/lib64:/lib"));

            plt0 = plt.CurrentLocation;
            pltgot.WriteNumber(0);
            if (plt.Is64Bit)
            {
                plt.Write(new byte[] { 0xff, 0x35 }); // push [rip+imm32]
                plt.WritePlaceholderDisplacement32(pltgot.CurrentLocation);
                pltgot.WriteNumber(0);
                plt.Write(new byte[] { 0xff, 0x25 }); // jmp [rip+imm32]
                plt.WritePlaceholderDisplacement32(pltgot.CurrentLocation);
                pltgot.WriteNumber(0);
            }
            else
            {
                plt.WriteByte(0x68); // push IMM32
                plt.WritePlaceholder(pltgot.CurrentLocation);
                pltgot.WriteNumber(0);
                plt.Write(new byte[] { 0xff, 0x25 }); // jmp [imm32]
                plt.WritePlaceholder(pltgot.CurrentLocation);
                pltgot.WriteNumber(0);
            }
            plt.Align(16, 0x90); // nop

        }

        public void Close()
        {
            dynsym.WriteHash();
            dynstr.WriteTo(dynstrRegion);
            mainRegion.WriteNumber(23); // dt_jmprel
            mainRegion.WritePlaceholder(relapltRegion.BaseLocation);
            mainRegion.WriteNumber(2); // dt_jmprelsz
            mainRegion.WriteNumber(relapltRegion.Length);
            mainRegion.WriteNumber(20); // dt_pltrel
            mainRegion.WriteNumber(7); // use rela

            mainRegion.WriteNumber(10); // dt_strsz
            mainRegion.WriteNumber(dynstrRegion.Length);
            mainRegion.WriteNumber(0); //DT_NULL
            mainRegion.WriteNumber(0);
            dynamicTokenSize.SetValue(mainRegion.Length);
        }

        public override void WriteGlobalSymbol(string name, Placeholder data)
        {
            dynsym.Write(data, name, 8);
        }

        public override Placeholder FetchImport(string namespaceName, string className, string fieldName)
        {
            return FetchImportAsPointer(namespaceName, (className + "__" + fieldName).Replace('.', '_'));
        }

        public override Placeholder FetchImportAsPointer(string library, string entryPoint)
        {
            SetupEntry(library, entryPoint);
            return pltgotEntry[entryPoint];
        }

        private void SetupEntry(string library, string entryPoint)
        {
            if (!libraries.Contains(library))
            {
                Require.False(entryPoints.Contains(entryPoint));
                libraries.Add(library);
                mainRegion.WriteNumber(1); //DT_NEEDED
                if (!library.EndsWith(".so"))
                    library = "lib" + library.Replace('.', '-') + ".so";
                mainRegion.WriteNumber(dynstr.Get(library));
            }
            if (!entryPoints.Contains(entryPoint))
            {
                entryPoints.Add(entryPoint);
                Placeholder p = pltgot.CurrentLocation;
                int symbolIndex = dynsym.Write(Placeholder.Null, entryPoint, 16);
                plt.Align(16, 0x90); // nop

                pltgotEntry.Add(entryPoint, p);
                pltEntry.Add(entryPoint, plt.CurrentLocation);

                if (plt.Is64Bit)
                {
                    plt.Write(new byte[] { 0xff, 0x25 });  // jmp [rip+imm32]

                    plt.WritePlaceholderDisplacement32(p);

                    pltgot.WritePlaceholder(plt.CurrentLocation);

                    plt.WriteByte(0x68); // push IMMS32
                    plt.WriteInt32(relocator.AddJumpSlot(p, symbolIndex));

                    plt.WriteByte(0xe9);  // jmp rip+imm32
                    plt.WritePlaceholderDisplacement32(plt0);
                }
                else
                {
                    plt.Write(new byte[] { 0xff, 0x25 });  // jmp [imm32]

                    plt.WritePlaceholder(p);

                    pltgot.WritePlaceholder(plt.CurrentLocation);

                    plt.WriteByte(0x68); // push IMMS32
                    plt.WriteInt32(relocator.AddJumpSlot(p, symbolIndex));

                    plt.WriteByte(0xe9);  // jmp imm32
                    plt.WritePlaceholder(plt0);
                }
            }
        }
    }
}
