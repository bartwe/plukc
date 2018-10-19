using System;
namespace Compiler
{
    public interface ILocation
    {
        int Line { get; }
        int Column { get; }
        string Source { get; }
    }

    public class NowhereLocation : ILocation
    {
        public int Line { get { return -1; } }
        public int Column { get { return -1; } }
        public string Source { get { return "-nowhere-"; } }
    }
}
