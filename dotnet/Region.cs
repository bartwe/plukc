using System;
using System.Collections.Generic;
using System.IO;

namespace Compiler
{
    public class Region : IDisposable
    {
        private struct RegionPlaceholder
        {
            public Placeholder target;
            public PlaceholderRef targetRef;
            public Placeholder location;
            public long offset;
            public bool relative; // if relative then the imagebase should not be added to the offset
            public bool relativeSection; // if enabled then give the value relative to the base of the target (dbg)
            public bool relativeFile; // if enabled then give the value relative to offset in file
            public bool displacement32; // if enabled then give the 32 displacement from the location to the target placeholder

            public Placeholder Target { get { if (targetRef == null) return target; return targetRef.Placeholder; } }
        }

        private MemoryStream stream = new MemoryStream();
        private List<RegionPlaceholder> placeholders = new List<RegionPlaceholder>();
        private long memoryLocation;
        private int fileLocation;
        private long sectionBase;
        private int sectionNumber;
        private bool empty;
        private bool _64bit;

        public int Length { get { return (int)stream.Length; } }
        public Placeholder BaseLocation { get { return new Placeholder(this, 0); } }
        public Placeholder CurrentLocation { get { return new Placeholder(this, stream.Position); } }
        internal long MemoryLocation { get { Require.True(memoryLocation > 0); return memoryLocation; } set { Require.True(memoryLocation == 0); Require.True(value > 0); memoryLocation = value; } }
        internal int FileLocation { get { Require.True(fileLocation > 0); return fileLocation; } set { Require.True(fileLocation == 0); Require.True(value > 0); fileLocation = value; } }
        internal long SectionBase { get { return sectionBase; } set { sectionBase = value; } }
        internal int SectionNumber { get { return sectionNumber; } }
        internal bool Empty { get { return empty; } }
        internal bool Is64Bit { get { return _64bit; } }
        internal int SizeOfWord { get { return Is64Bit ? 8 : 4; } }

        internal Region(int sectionNumber, bool _64bit)
        {
            this._64bit = _64bit;
            this.sectionNumber = sectionNumber;
        }

        public void MarkEmpty()
        {
            empty = true;
        }

        internal void ResolvePlaceholders(long imageBase)
        {
            foreach (RegionPlaceholder placeholder in placeholders)
            {
                if (placeholder.displacement32)
                {
                    stream.Position = placeholder.offset;
                    WriteInt32(placeholder.Target.ActualMemoryDistanceFrom(placeholder.location));
                }
                else
                {
                    Region region = placeholder.Target.Region;
                    long regionOffset = placeholder.Target.Offset;
                    long value;
                    if (placeholder.relativeFile)
                    {
                        value = regionOffset + region.FileLocation;
                    }
                    else
                    {
                        value = regionOffset + region.MemoryLocation;
                        if (placeholder.relative)
                            value -= imageBase;
                        if (placeholder.relativeSection)
                            value -= region.SectionBase;
                    }
                    stream.Position = placeholder.offset;
                    WriteNumber(value);
                }
            }
        }

        public void WriteULEB(long data)
        {
            Require.True(data >= 0);
            while (true)
            {
                byte x = (byte)(data & 0x7f);
                data = data >> 7;
                if (data != 0)
                    x |= 0x80;
                WriteByte(x);
                if (data == 0)
                    break;
            }
        }

        public void WriteSLEB(long data)
        {
            bool more = true;
            bool negative = data < 0;
            int size = 64;
            while (more)
            {
                byte x = (byte)(data & 0x7f);
                data = data >> 7;
                if (negative)
                {
                    long mask = -(1 << (size - 7));
                    data |= mask;
                }

                if (((data == 0) && ((x & 0x40) == 0)) ||
                    ((data == -1) && ((x & 0x40) != 0)))
                    more = false;
                else
                    x |= 0x80;
                WriteByte(x);
            }
        }

        public void WriteByte(byte data)
        {
            stream.WriteByte(data);
        }

        public void Write(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            stream.Write(data, 0, data.Length);
        }

        public void Write(byte[] data, int offset, int length)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            stream.Write(data, offset, length);
        }

        public WordToken InsertWordToken()
        {
            WordToken token = new WordToken(stream);
            WriteInt16(0);
            return token;
        }

        public IntToken InsertIntToken()
        {
            IntToken token = new IntToken(stream, CurrentLocation);
            WriteInt32(0);
            return token;
        }

        public LongToken InsertLongToken()
        {
            LongToken token = new LongToken(stream, CurrentLocation);
            WriteInt64(0);
            return token;
        }

        public NumberToken InsertNumberToken()
        {
            if (Is64Bit)
                return new NumberToken(InsertLongToken());
            else
                return new NumberToken(InsertIntToken());
        }

        public void WriteNumberTimesBitness(long data)
        {
            if (_64bit)
                WriteInt64(data * 8);
            else
                WriteInt32(data * 4);
        }

        public void WriteNumber(long data)
        {
            if (_64bit)
                WriteInt64(data);
            else
                WriteInt32(data);
        }

        byte[] _buffer = new byte[8];

        public void WriteInt64(long data)
        {
            unchecked
            {
                _buffer[0] = (byte)((data >> 0) & 0xff);
                _buffer[1] = (byte)((data >> 8) & 0xff);
                _buffer[2] = (byte)((data >> 16) & 0xff);
                _buffer[3] = (byte)((data >> 24) & 0xff);
                _buffer[4] = (byte)((data >> 32) & 0xff);
                _buffer[5] = (byte)((data >> 40) & 0xff);
                _buffer[6] = (byte)((data >> 48) & 0xff);
                _buffer[7] = (byte)((data >> 56) & 0xff);
                stream.Write(_buffer, 0, 8);
            }
        }

        public void WriteInt32(long data)
        {
            unchecked
            {
                _buffer[0] = (byte)((data >> 0) & 0xff);
                _buffer[1] = (byte)((data >> 8) & 0xff);
                _buffer[2] = (byte)((data >> 16) & 0xff);
                _buffer[3] = (byte)((data >> 24) & 0xff);
                stream.Write(_buffer, 0, 4);
            }
        }

        public void WriteInt16(short data)
        {
            unchecked
            {
                _buffer[0] = (byte)((data >> 0) & 0xff);
                _buffer[1] = (byte)((data >> 8) & 0xff);
                stream.Write(_buffer, 0, 2);
            }
        }

        public void WriteInt8(long data)
        {
            unchecked
            {
                stream.WriteByte((byte)((data >> 0) & 0xff));
            }
        }

        public void WriteAsUtf8NullTerminated2(string data)
        {
            byte[] bytes = (new System.Text.UTF8Encoding()).GetBytes(data);
            Write(bytes);
            WriteByte(0);
            if (bytes.Length % 2 == 0)
                WriteByte(0); // keep at even length
        }

        public void WriteAsUtf8NullTerminated(string data)
        {
            byte[] bytes = (new System.Text.UTF8Encoding()).GetBytes(data);
            Write(bytes);
            WriteByte(0);
        }

        public void WriteAsUtf16NullTerminated2(string data)
        {
            byte[] bytes = (new System.Text.UnicodeEncoding()).GetBytes(data);
            Write(bytes);
            WriteInt16(0);
        }

        private RegionPlaceholder SetupPlaceholder(Placeholder target)
        {
            Require.Assigned(target);
            RegionPlaceholder placeholder;
            placeholder.offset = stream.Position;
            placeholder.target = target;
            placeholder.targetRef = null;
            placeholder.relative = false;
            placeholder.relativeSection = false;
            placeholder.relativeFile = false;
            placeholder.displacement32 = false;
            WriteNumber(0);
            placeholder.location = CurrentLocation;
            return placeholder;
        }

        private RegionPlaceholder SetupPlaceholderRef(PlaceholderRef target)
        {
            Require.Assigned(target);
            RegionPlaceholder placeholder;
            placeholder.offset = stream.Position;
            placeholder.target = new Placeholder();
            placeholder.targetRef = target;
            placeholder.relative = false;
            placeholder.relativeSection = false;
            placeholder.relativeFile = false;
            placeholder.displacement32 = false;
            WriteNumber(0);
            placeholder.location = CurrentLocation;
            return placeholder;
        }

        public void WritePlaceholder(Placeholder target)
        {
            RegionPlaceholder placeholder = SetupPlaceholder(target);
            placeholders.Add(placeholder);
        }

        public void WritePlaceholderRef(PlaceholderRef target)
        {
            RegionPlaceholder placeholder = SetupPlaceholderRef(target);
            placeholders.Add(placeholder);
        }

        public void WritePlaceholderRelative(Placeholder target)
        {
            RegionPlaceholder placeholder = SetupPlaceholder(target);
            placeholder.relative = true;
            placeholders.Add(placeholder);
        }

        public void WritePlaceholderFile(Placeholder target)
        {
            RegionPlaceholder placeholder = SetupPlaceholder(target);
            placeholder.relativeFile = true;
            placeholders.Add(placeholder);
        }

        public void WritePlaceholderRelativeRegion(Placeholder target)
        {
            RegionPlaceholder placeholder = SetupPlaceholder(target);
            placeholder.relativeSection = true;
            placeholders.Add(placeholder);
        }

        public void WritePlaceholderDisplacement32(Placeholder target)
        {
            Require.Assigned(target);
            RegionPlaceholder placeholder;
            placeholder.offset = stream.Position;
            placeholder.target = target;
            placeholder.targetRef = null;
            placeholder.relative = false;
            placeholder.relativeSection = false;
            placeholder.relativeFile = false;
            placeholder.displacement32 = true;
            WriteInt32(0);
            placeholder.location = CurrentLocation;
            placeholders.Add(placeholder);
        }

        public void Align(int alignment, byte fill)
        {
            Require.True(alignment >= 1);
            Require.True(alignment <= 16);
            while ((stream.Position % alignment) != 0)
                WriteByte(fill);
        }

        public void WriteToStream(Stream stream)
        {
            this.stream.WriteTo(stream);
        }

        public void WriteToRegion(Region output)
        {
            stream.WriteTo(output.stream);
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
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class WordToken
    {
        private Stream stream;
        private long position;

        internal WordToken(Stream stream)
        {
            this.stream = stream;
            position = stream.Position;
        }

        public void SetValue(int data)
        {
            Require.True(data <= short.MaxValue);
            Require.True(data >= short.MinValue);
            long pos = stream.Position;
            stream.Position = position;
            unchecked
            {
                stream.WriteByte((byte)((data >> 0) & 0xff));
                stream.WriteByte((byte)((data >> 8) & 0xff));
            }
            stream.Position = pos;
        }

        public void SetDistanceSinceTaken()
        {
            SetValue((int)(stream.Position - position));
        }
    }

    public class IntToken
    {
        private Stream stream;
        private long position;
        private Placeholder location;

        internal IntToken(Stream stream, Placeholder location)
        {
            this.stream = stream;
            position = stream.Position;
            this.location = location;
        }

        public Placeholder Location { get { return location; } }

        public void SetValue(int data)
        {
            long pos = stream.Position;
            stream.Position = position;
            unchecked
            {
                stream.WriteByte((byte)((data >> 0) & 0xff));
                stream.WriteByte((byte)((data >> 8) & 0xff));
                stream.WriteByte((byte)((data >> 16) & 0xff));
                stream.WriteByte((byte)((data >> 24) & 0xff));
            }
            stream.Position = pos;
        }
    }

    public class LongToken
    {
        private Stream stream;
        private long position;
        private Placeholder location;

        internal LongToken(Stream stream, Placeholder location)
        {
            this.stream = stream;
            position = stream.Position;
            this.location = location;
        }

        public Placeholder Location { get { return location; } }

        public void SetValue(long data)
        {
            long pos = stream.Position;
            stream.Position = position;
            unchecked
            {
                stream.WriteByte((byte)((data >> 0) & 0xff));
                stream.WriteByte((byte)((data >> 8) & 0xff));
                stream.WriteByte((byte)((data >> 16) & 0xff));
                stream.WriteByte((byte)((data >> 24) & 0xff));
                stream.WriteByte((byte)((data >> 32) & 0xff));
                stream.WriteByte((byte)((data >> 40) & 0xff));
                stream.WriteByte((byte)((data >> 48) & 0xff));
                stream.WriteByte((byte)((data >> 56) & 0xff));
            }
            stream.Position = pos;
        }
    }

    public class NumberToken
    {
        private LongToken ltoken;
        private IntToken itoken;

        public NumberToken(LongToken ltoken)
        {
            this.ltoken = ltoken;
        }
        public NumberToken(IntToken itoken)
        {
            this.itoken = itoken;
        }
        public void SetValue(long data)
        {
            if (ltoken != null)
                ltoken.SetValue(data);
            else
                itoken.SetValue((int)data);
        }
    }
}
