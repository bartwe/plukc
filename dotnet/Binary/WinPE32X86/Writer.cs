using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace Compiler.Binary.WinPE32X86
{
    public class Writer : IDisposable
    {
        private Dictionary<string, List<Region>> regionCategories = new Dictionary<string, List<Region>>();

        private Region debugLocations;
        private Region debugRegion;
        private List<Region> regions = new List<Region>();
        private Dictionary<string, int> sectionNumbers = new Dictionary<string, int>();
        private bool resolved;

        public Region DebugLocations { get { return debugLocations; } }
        public Region DebugSymbols { get { return debugRegion; } }

        public Writer()
        {
            debugRegion = new Region(-1, false);
            debugLocations = new Region(-1, false);
        }

        public void RegisterSection(string name, int number)
        {
            Require.False(sectionNumbers.ContainsKey(name));
            sectionNumbers[name] = number;
        }

        public Region AllocateRegion(string kind)
        {
            Region region = new Region(sectionNumbers[kind], false);
            AddRegion(kind, region);
            regions.Add(region);
            return region;
        }

        public int Length(string category, int alignment)
        {
            Require.True(alignment >= 1);
            Require.True(alignment <= 16);
            int total = 0;
            foreach (Region region in regionCategories[category])
            {
//                if ((region.Length == 0) != region.Empty)
//                    throw new InvalidOperationException("A region was found to be empty, but not marked as such.");
                total += region.Length;
                while ((total % alignment) != 0)
                    total += alignment - (total % alignment);
            }
            return total;
        }

        public void Place(string kind, int memoryOffset/*imageBase*/, int fileOffset, int alignment)
        {
            int sectionBaseMemoryOffset = memoryOffset;
            int offset = memoryOffset;
            int delta = fileOffset - memoryOffset;
            foreach (Region region in regionCategories[kind])
            {
                region.MemoryLocation = offset;
                region.FileLocation = offset + delta;
                region.SectionBase = sectionBaseMemoryOffset;
                offset += region.Length;
                while ((offset % alignment) != 0)
                    offset += alignment - (offset % alignment);
            }
        }

        public void ResolvePlaceholders(int imageBase)
        {
            Require.False(resolved);
            resolved = true;
            foreach (Region region in regions)
                region.ResolvePlaceholders(imageBase);
            debugRegion.ResolvePlaceholders(imageBase);
            debugLocations.ResolvePlaceholders(imageBase);
        }

        public void WriteToStream(string kind, byte fill, Stream stream, int alignment)
        {
            Require.True(resolved);
            foreach (Region region in regionCategories[kind])
            {
                while ((stream.Position % alignment) != 0)
                    stream.WriteByte(fill);
                region.WriteToStream(stream);
            }
        }

        private void AddRegion(string category, Region region)
        {
            List<Region> regions;
            if (!regionCategories.TryGetValue(category, out regions))
            {
                regions = new List<Region>();
                regionCategories[category] = regions;
            }
            regions.Add(region);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (debugRegion != null)
                {
                    debugRegion.Dispose();
                    debugRegion = null;
                }
                if (debugLocations != null)
                {
                    debugLocations.Dispose();
                    debugLocations = null;
                }
                if (regions != null)
                {
                    foreach (Region region in regions)
                        region.Dispose();
                    regions = null;
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
