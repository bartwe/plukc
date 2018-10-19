using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Binary.LinuxELF
{
    struct SourceLine
    {
        public Placeholder placeholder;
        public ILocation location;
        public SourceMark mark;

        public SourceLine(Placeholder placeholder, ILocation location, SourceMark mark)
        {
            this.placeholder = placeholder;
            this.location = location;
            this.mark = mark;
        }

        public static bool Match(SourceLine l, SourceLine r)
        {
            if (l.mark != r.mark) return false;
            if (l.location.Line != r.location.Line) return false;
            if (l.location.Column != r.location.Column) return false;
            if (l.location.Source != r.location.Source) return false;
            if (l.placeholder.Region != r.placeholder.Region) return false;
            if (l.placeholder.Offset != r.placeholder.Offset) return false;
            return true;
        }
    }

    class Symbols : Compiler.Symbols
    {
        private Region output;
        StringTable strtabl;
        private Region debuginfo;
        private Region debugabbrev;
        private Region debugline;
        private Dictionary<string, List<SourceLine>> sourceLines = new Dictionary<string, List<SourceLine>>();


        public Symbols(Sections sections, StringTable strings)
        {
            strtabl = strings;
            output = sections.GetSection(".symtab").AllocateRegion();
            Sym("", Placeholder.Null, 0, 0, 0, 0);
            debuginfo = sections.GetSection(".debug_info").AllocateRegion();
            debugabbrev = sections.GetSection(".debug_abbrev").AllocateRegion();
            debugline = sections.GetSection(".debug_line").AllocateRegion();
        }

        public override void Source(Placeholder placeholder, ILocation location, SourceMark mark)
        {
            if (location.Source == "-nowhere-")
                return;
            List<SourceLine> locations;
            if (!sourceLines.TryGetValue(location.Source, out locations))
            {
                locations = new List<SourceLine>();
                sourceLines.Add(location.Source, locations);
            }
            locations.Add(new SourceLine(placeholder, location, mark));
        }

        public override void Close()
        {

            debugabbrev.WriteULEB(1);
            debugabbrev.WriteULEB(0x11); // compile_unit
            debugabbrev.WriteByte(0); // no children
            debugabbrev.WriteULEB(0x10); // stmt_list
            debugabbrev.WriteULEB(0x6);
            debugabbrev.WriteULEB(0x12); // high_addr
            debugabbrev.WriteULEB(0x1);
            debugabbrev.WriteULEB(0x11); // low_addr
            debugabbrev.WriteULEB(0x1);
            debugabbrev.WriteULEB(0x3); // name
            debugabbrev.WriteULEB(0x8);
            debugabbrev.WriteULEB(0x25); // producer
            debugabbrev.WriteULEB(0x8);
            debugabbrev.WriteByte(0x0); // terminator
            debugabbrev.WriteByte(0x0);

            debugabbrev.WriteByte(0x0);
            foreach (string file in sourceLines.Keys)
            {
                List<SourceLine> list = sourceLines[file];
                Placeholder lowAddr = list[0].placeholder;
                Placeholder highAddr = list[list.Count - 1].placeholder;
                IntToken cu_length = debuginfo.InsertIntToken(); // cu_length;
                Placeholder cu_length_begin = debuginfo.CurrentLocation;
                debuginfo.WriteInt16(2);
                debuginfo.WriteInt32(0);
                debuginfo.WriteInt8(debuginfo.SizeOfWord);

                debuginfo.WriteULEB(1); // DW_TAG_compile_unit
                debuginfo.WriteInt32((int)debugline.CurrentLocation.Offset);
                debuginfo.WritePlaceholder(highAddr);
                debuginfo.WritePlaceholder(lowAddr);
                debuginfo.WriteAsUtf8NullTerminated(file);
                debuginfo.WriteAsUtf8NullTerminated("pluk plux0r 1.2.3");
                cu_length.SetValue((int)(debuginfo.CurrentLocation.MemoryDistanceFrom(cu_length_begin)));

                IntToken total_length = debugline.InsertIntToken();
                Placeholder begin = debugline.CurrentLocation;
                debugline.WriteInt16(2); //version
                IntToken prologue_length = debugline.InsertIntToken();
                Placeholder prologue = debugline.CurrentLocation;
                debugline.WriteByte(1); // minimum_instruction_length
                debugline.WriteByte(1); // default_is_stmt
                debugline.Write(new byte[] { 256 - 5, 14, 10, 0, 1, 1, 1, 1, 0, 0, 0, 1 });

                string directory = System.IO.Path.GetDirectoryName(file);
                string filename = System.IO.Path.GetFileName(file);

                if (string.IsNullOrEmpty(directory))
                    directory = ".";
                debugline.WriteAsUtf8NullTerminated(directory);
                debugline.WriteByte(0);
                Require.NotEmpty(file);
                debugline.WriteAsUtf8NullTerminated(filename);
                debugline.Write(new byte[] { 1, 0, 0 });
                debugline.WriteByte(0);

                prologue_length.SetValue((int)(debugline.CurrentLocation.MemoryDistanceFrom(prologue)));

                int line = 1;
                int column = 0;
                SourceLine prev = new SourceLine();
                foreach (SourceLine x in list)
                {
                    if (SourceLine.Match(prev, x))
                    {
                        prev = x;
                        continue;
                    }
                    prev = x;
                    debugline.WriteByte(0); // DW_LNE_set_address
                    debugline.WriteULEB(1 + debugline.SizeOfWord);
                    debugline.WriteByte(2);
                    debugline.WritePlaceholder(x.placeholder);

                    int l = x.location.Line;
                    if (l < 0) l = 0;
                    int delta = l - line;
                    line = l;
                    if (delta != 0)
                    {
                        debugline.WriteByte(3); // advance_line
                        debugline.WriteSLEB(delta);
                    }
                    if (x.location.Column != column)
                    {
                        debugline.WriteByte(5); // set_column
                        if (x.location.Column < 0)
                            column = 0;
                        else
                            column = x.location.Column;
                        debugline.WriteULEB(column);
                    }
                    if (x.mark == SourceMark.EndSequence)
                    {
                        debugline.Write(new byte[] { 0, 1, 1 }); // end_sequence
                        line = 1;
                        column = 0;
                    }
                    else
                        debugline.WriteByte(1); // copy
                }
                debugline.Write(new byte[] { 0, 1, 1 }); // end_sequence
                total_length.SetValue((int)(debugline.CurrentLocation.MemoryDistanceFrom(begin)));
            }
        }

        public override void WriteCode(Placeholder location, long length, string token)
        {
            Sym(token, location, length, 18, 0, (short)location.Region.SectionNumber);
        }

        public override void WriteData(Placeholder location, long length, string token)
        {
            Sym(token, location, length, 17, 0, (short)location.Region.SectionNumber);
        }

        private void Sym(string name, Placeholder location, long size, byte info, byte other, short shndx)
        {
            Require.True((location.IsNull) || (shndx > 0));
            output.WriteInt32(strtabl.Get(name));
            if (output.Is64Bit)
            {
                output.WriteByte(info);
                output.WriteByte(other);
                output.WriteInt16(shndx);
            }
            if (location.IsNull)
                output.WriteNumber(0);
            else
                output.WritePlaceholder(location);
            output.WriteNumber(size);
            if (!output.Is64Bit)
            {
                output.WriteByte(info);
                output.WriteByte(other);
                output.WriteInt16(shndx);
            }
        }
    }
}
