using System;

namespace Compiler
{
    public struct Placeholder : IEquatable<Placeholder>
    {
        public static Placeholder Null { get { return default(Placeholder); } }

        private Region region;
        private long offset;

        public Region Region { get { return region; } }
        public long Offset { get { return offset; } }

        public bool IsNull
        {
            get
            {
                return (region == null) && (offset == 0);
            }
        }

        public Placeholder(Region region, long offset)
        {
            Require.Assigned(region);
            this.region = region;
            this.offset = offset;
        }

        public Placeholder Increment(long offset)
        {
            return new Placeholder(region, this.offset + offset);
        }

        public long MemoryDistanceFrom(Placeholder other)
        {
            Require.True(region == other.region);
            return offset - other.offset;
        }

        public long ActualMemoryDistanceFrom(Placeholder other)
        {
            //these requires might be wrong for some specific cases
            Require.True(region.MemoryLocation != 0);
            Require.True(other.region.MemoryLocation != 0);
            return (region.MemoryLocation + offset) - (other.region.MemoryLocation + other.offset);
        }

        public bool Equals(Placeholder other) {
            return (offset == other.offset) && (region == other.region);
        }

        public override bool Equals(object obj) {
            Placeholder other = (Placeholder)obj;
            return (offset == other.offset) && (region == other.region);
        }

        public override int GetHashCode() {
            return region.GetHashCode() ^ (unchecked((int)offset) ^ (int)(offset >> 32));
        }
    }

    public class PlaceholderRef
    {
        Placeholder placeholder;
        public Placeholder Placeholder { get { return placeholder; } set { placeholder = value; } }
    }
}
