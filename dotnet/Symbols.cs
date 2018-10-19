using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public enum SourceMark { Internal, Normal, EndSequence }
    public abstract class Symbols
    {
        public void Source(Placeholder placeholder, ILocation location)
        {
            Source(placeholder, location, SourceMark.Normal);
        }

        public abstract void Source(Placeholder placeholder, ILocation location, SourceMark mark);
        public abstract void Close();
        public abstract void WriteCode(Placeholder location, long length, string token);
        public abstract void WriteData(Placeholder location, long length, string token);
    }
}
