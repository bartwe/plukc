using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.LinuxELF
{
    public class DynamicSymbols
    {
        private Dictionary<string, int> hash = new Dictionary<string, int>();
        private int entryCount;

        private StringTable dynstr;
        private Section dynsym;
        private Region output;
        private Region hashRegion;

        public Region HashRegion { get { return hashRegion; } }
        public Region SymbolRegion { get { return output; } }
        public long ElementSize { get { return 8 + 2 * (output.Is64Bit ? 8 : 4); } }

        public DynamicSymbols(Sections sections, StringTable dynstr)
        {
            this.dynstr = dynstr;
            dynsym = sections.GetSection(".dynsym");
            output = dynsym.AllocateRegion();
            hashRegion = sections.GetSection(".hash").AllocateRegion();
            NumberToken s = Sym("", Placeholder.Null, 0, 0, 0);
            s.SetValue(0);
        }

        public void WriteHash()
        {
            LinkedList<int> freelist = new LinkedList<int>();

            int count = hash.Count;
            int[] bucket = new int[count];
            int[] chain = new int[count];
            foreach (KeyValuePair<string, int> entry in hash)
            {
                int h = elf_hash(entry.Key) % count;
                if (bucket[h] == 0)
                    bucket[h] = entry.Value;
            }
            for (int i = 0; i < bucket.Length; ++i)
                if (bucket[i] == 0)
                    freelist.AddLast(i);
            foreach (KeyValuePair<string, int> entry in hash)
            {
                long h = elf_hash(entry.Key) % count;
                if (bucket[h] == entry.Value)
                    continue;
                while (true)
                {
                    if (bucket[h] == 0)
                    {
                        bucket[h] = entry.Value;
                        break;
                    }
                    if (chain[h] == 0)
                    {
                        chain[h] = freelist.Last.Value;
                        freelist.RemoveLast();
                    }
                    h = chain[h];
                };
            }
            hashRegion.WriteInt32(bucket.Length);
            hashRegion.WriteInt32(chain.Length);
            foreach (long b in bucket)
                hashRegion.WriteInt32(b);
            foreach (long c in chain)
                hashRegion.WriteInt32(c);
        }

        public NumberToken Write(Placeholder location, string token)
        {
            return Sym(token, location, 16, 0, 0);
        }

        public int Write(Placeholder location, string token, long size)
        {
            NumberToken lt = Sym(token, location, 16, 0, 0);
            lt.SetValue(size);
            return entryCount - 1;
        }

        private NumberToken Sym(string name, Placeholder location, byte info, byte other, short shndx)
        {
            output.WriteInt32(dynstr.Get(name));
            if (output.Is64Bit)
            {
                output.WriteByte(info);
                output.WriteByte(other);
                output.WriteInt16(shndx);
            }
            if (location.IsNull)
                output.WriteNumber(0);
            else
                output.WritePlaceholder(location);
            NumberToken result = output.InsertNumberToken();
            if (!output.Is64Bit)
            {
                output.WriteByte(info);
                output.WriteByte(other);
                output.WriteInt16(shndx);
            }

            hash[name] = entryCount;
            entryCount++;
            return result;
        }

        private static int elf_hash(string name)
        {
            byte[] bs = Encoding.UTF8.GetBytes(name);
            int h = 0;
            int g;
            unchecked
            {
                foreach (byte b in bs)
                {
                    h = (h << 4) + (int)b;
                    g = (int)((long)h & 0xf0000000);
                    if (g != 0)
                        h ^= g >> 24;
                    h &= 0x0fffffff;
                }
            }
            return h;
        }

    }
}
