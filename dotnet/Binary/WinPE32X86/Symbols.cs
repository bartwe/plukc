using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.WinPE32X86
{
    public class Symbols : Compiler.Symbols
    {
        private Region locationsRegion;
        private Region debugRegion;
        private Set<Placeholder> visitedPositions = new Set<Placeholder>();
        private Dictionary<string, Dictionary<int, Placeholder>> sourceLocations = new Dictionary<string, Dictionary<int, Placeholder>>();

        internal Symbols(Region locationsRegion, Region debugRegion)
        {
            this.locationsRegion = locationsRegion;
            this.debugRegion = debugRegion;
        }

        public override void Source(Placeholder placeholder, ILocation location, SourceMark mark)
        {
            Placeholder position = placeholder;
            Require.True(placeholder.Region.SectionNumber == 1);
            if (!visitedPositions.Contains(position))
            {
                visitedPositions.Add(position);
                if (!sourceLocations.ContainsKey(location.Source))
                    sourceLocations.Add(location.Source, new Dictionary<int, Placeholder>());
                Dictionary<int, Placeholder> lineToOffset = sourceLocations[location.Source];
                if (!lineToOffset.ContainsKey(location.Line))
                    lineToOffset.Add(location.Line, position);
            }
        }

        public override void Close()
        {
            //todo: support source locations for stuff outside of code segment ?
            //pagina 72
            Placeholder baseLocation = locationsRegion.CurrentLocation;
            locationsRegion.WriteInt16((short)sourceLocations.Count); // cFile, number of sourcefiles used
            locationsRegion.WriteInt16(1); // cSeg number of segments that hold code form these sourcefiles
            List<string> sourceList = new List<string>();
            List<IntToken> sourceTable = new List<IntToken>();
            List<IntToken> linenumberTable = new List<IntToken>();
            foreach (string source in sourceLocations.Keys)
            {
                sourceList.Add(source);
                sourceTable.Add(locationsRegion.InsertIntToken());
            }
            locationsRegion.WriteInt32(0); // seg0: start 
            locationsRegion.WriteInt32(0xffffffff); // seg0:  // end workaround: we do not know at this time what value it should have, it is optional according to the spec, but isn't
            locationsRegion.WriteInt16(1); // only code segment receives info
            locationsRegion.WriteInt16(0); // padding

            // per source table
            for (int i = 0; i < sourceList.Count; ++i)
            {
                string source = sourceList[i];
                sourceTable[i].SetValue((int)(locationsRegion.CurrentLocation.MemoryDistanceFrom(baseLocation)));
                locationsRegion.WriteInt16(1); // cSeg
                locationsRegion.WriteInt16(0); // padding
                linenumberTable.Add(locationsRegion.InsertIntToken()); // offset to linenumbertable
                locationsRegion.WriteInt32(0); // start
                locationsRegion.WriteInt32(0xffffffff); // end workaround: we do not know at this time what value it should have, it is optional according to the spec, but isn't
                // workaround, this string isn't according to the spec.
                byte[] bytes = (new System.Text.UTF8Encoding()).GetBytes(source);
                locationsRegion.WriteByte((byte)bytes.Length);
                locationsRegion.Write(bytes);
                int length = bytes.Length + 1;
                while ((length % 4) != 0)
                {
                    length++;
                    locationsRegion.WriteByte(0);
                }
            }

            // per source linenumbers table
            for (int i = 0; i < sourceList.Count; ++i)
            {
                string source = sourceList[i];
                linenumberTable[i].SetValue((int)(locationsRegion.CurrentLocation.MemoryDistanceFrom(baseLocation)));
                locationsRegion.WriteInt16(1); // code segment
                Dictionary<int, Placeholder> linenumbers = sourceLocations[source];
                locationsRegion.WriteInt16((short)linenumbers.Count);
                List<int> linenumbersSorted = new List<int>();
                linenumbersSorted.AddRange(linenumbers.Keys);
                linenumbersSorted.Sort();
                foreach (int linenumber in linenumbersSorted)
                    locationsRegion.WritePlaceholderRelativeRegion(linenumbers[linenumber]); // TODO: find out what kind of pointer is needed here
                foreach (int linenumber in linenumbersSorted)
                    locationsRegion.WriteInt16((short)linenumber);
                if (linenumbersSorted.Count % 2 == 1)
                    locationsRegion.WriteInt16(0); // allign
            }

        }

        public override void WriteCode(Placeholder location, long length, string token)
        {
            Write(location, length, token);
        }

        public override void WriteData(Placeholder location, long length, string token)
        {
            Write(location, length, token);
        }

        private void Write(Placeholder location, long length, string token)
        {
            if (location.IsNull)
                throw new ArgumentNullException("location");
            if (string.IsNullOrEmpty(token))
                throw new ArgumentOutOfRangeException("token");
            Require.True(location.Region.SectionNumber >= 1);
            byte[] bytes = (new System.Text.UTF8Encoding()).GetBytes(token);
            int baselength = 10 + 1 + bytes.Length;
            int strlength = baselength;
            while ((strlength % 4) != 0)
                strlength++;
            debugRegion.WriteInt16((short)strlength);
            debugRegion.WriteInt16(0x203); // S_PUB32
            debugRegion.WritePlaceholderRelativeRegion(location);
            debugRegion.WriteInt16((short)location.Region.SectionNumber);
            debugRegion.WriteInt16(0);
            debugRegion.WriteByte((byte)Math.Min(255, bytes.Length));
            debugRegion.Write(bytes, 0, Math.Min(255, bytes.Length));
            for (int i = 0; i < (strlength - baselength); ++i)
                debugRegion.WriteByte(0);
        }
    }
}
