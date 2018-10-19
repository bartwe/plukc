using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.LinuxELF
{
    public class Sections
    {
        private Dictionary<string, Section> sections = new Dictionary<string, Section>();
        private StringTable stringTable;
        private bool is64bit;

        public int Count { get { return sections.Count; } }

        public IEnumerable<Section> Children
        {
            get
            {
                return sections.Values;
            }
        }

        public Sections(StringTable stringTable, bool is64bit)
        {
            this.is64bit = is64bit;
            this.stringTable = stringTable;
            sections[""] = new Section("", 0, 16, is64bit);
        }

        public void RegisterSection(string name, int index)
        {
            if (sections.ContainsKey(name))
                throw new Exception("Can only register a section once.: " + name);

            stringTable.Get(name);
            Section result = new Section(name, index, 16, is64bit);
            sections[name] = result;
        }

        public Section GetSection(string name)
        {
            if (!sections.ContainsKey(name))
                throw new Exception("Unknown section.:" + name);
            return sections[name];
        }

        public void ResolvePlaceholders(long imageBase)
        {
            foreach (Section region in this.sections.Values)
                region.ResolvePlaceholders(imageBase);
        }
    }
}
