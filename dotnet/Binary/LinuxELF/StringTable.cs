// StringTable.cs created with MonoDevelop
// User: bartwe at 7:00 PM 8/18/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

namespace Compiler.Binary.LinuxELF
{
    public class StringTable
    {
        private int index;
        private List<string> data = new List<string>();
        private Dictionary<string, int> mapping = new Dictionary<string, int>();
        private bool locked;

        public StringTable()
        {
            Get("");
        }

        public void WriteTo(Region region)
        {
            locked = true;
            foreach (string s in data)
                region.WriteAsUtf8NullTerminated(s);
        }

        public int Get(string data)
        {
            if (mapping.ContainsKey(data))
                return mapping[data];
            Require.False(locked);
            mapping[data] = index;
            this.data.Add(data);
            int length = Encoding.UTF8.GetByteCount(data);
            if (length < 16)
                for (int i = 1; i < length; ++i)
                {
                    string s = data.Substring(i);
                    if (mapping.ContainsKey(s))
                        break;
                    mapping[s] = index + i;
                }
            int result = index;
            index += length + 1;
            return result;
        }
    }
}
