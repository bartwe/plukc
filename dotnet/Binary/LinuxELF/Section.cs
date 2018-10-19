using System;
using System.Collections.Generic;
using System.IO;

namespace Compiler.Binary.LinuxELF
{
    public class Section
    {
        private List<Region> regions = new List<Region>();
        private bool is64bit;

        private string name;
        private int index;
        private long regionAlignment;
        private long memoryAddress;
        private long fileOffset;

        public string Name { get { return name; } }
        public int Index { get { return index; } }
        public long RegionAlignment { get { return regionAlignment; } }
        public long MemoryAddress { get { return memoryAddress; } }
        public long FileOffset { get { return fileOffset; } set { fileOffset = value; } }

        public Section(string name, int index, long alignment, bool is64bit)
        {
            this.is64bit = is64bit;
            this.name = name;
            this.index = index;
            regionAlignment = alignment;
        }

        public Region AllocateRegion()
        {
            Region r = new Region(Index, is64bit);
            regions.Add(r);
            return r;
        }

        public long AlignMemoryAddress(long address)
        {
            if ((address % RegionAlignment) != 0)
                address += RegionAlignment - (address % RegionAlignment);
            return address;
        }

        public long Place(long memoryAddress)
        {
            long address = memoryAddress;
            this.memoryAddress = memoryAddress;
            foreach (Region region in regions)
            {
                if ((address % RegionAlignment) != 0)
                    address += RegionAlignment - (address % RegionAlignment);
                region.MemoryLocation = address;
                region.SectionBase = memoryAddress;
                address += region.Length;
            }
            return address;
        }

        public long FakePlace(long memoryAddress)
        {
            long address = memoryAddress;
            this.memoryAddress = memoryAddress;
            foreach (Region region in regions)
            {
                if ((address % RegionAlignment) != 0)
                    address += RegionAlignment - (address % RegionAlignment);
                address += region.Length;
            }
            return address;
        }

        public void AlignFileOffset(Region output)
        {
            while ((output.Length % RegionAlignment) != 0)
                output.WriteByte(0);
        }

        public void WriteToRegion(Region output, byte fill)
        {
            foreach (Region region in regions)
            {
                while ((output.Length % RegionAlignment) != 0)
                    output.WriteByte(fill);
                region.WriteToRegion(output);
            }
        }

        public long Length
        {
            get
            {
                long result = 0;
                foreach (Region r in regions)
                {
                    while ((result % RegionAlignment) != 0)
                        result++;
                    result += r.Length;
                }
                return result;
            }
        }

        internal void ResolvePlaceholders(long imageBase)
        {
            foreach (Region region in regions)
                region.ResolvePlaceholders(imageBase);
        }

    }
}
