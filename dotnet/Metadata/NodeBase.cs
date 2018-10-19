using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class NodeBase : ILocation
    {
        string source;
        int line;
        int column;

        public string Source { get { return source; } }
        public int Line { get { return line; } }
        public int Column { get { return column; } }

        public NodeBase(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            UpdateMetadataBase(location);
        }

        public void UpdateMetadataBase(ILocation location)
        {
            if (location == null)
                throw new ArgumentNullException("location");
            this.source = location.Source;
            this.line = location.Line;
            this.column = location.Column;
        }
    }
}
