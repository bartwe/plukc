// Relocator.cs created with MonoDevelop
// User: bartwe at 9:16 PM 8/29/2008
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;

namespace Compiler.Binary.LinuxELF
{
    public class Relocator
    {
        private Region relocations;
        private int index;

        public Relocator(Region relocations)
        {
            this.relocations = relocations;
        }

        public int AddJumpSlot(Placeholder location, int symbol)
        {
            relocations.WritePlaceholder(location);
            if (relocations.Is64Bit)
            {
                relocations.WriteInt32(7); // jmp_slot
                relocations.WriteInt32(symbol); // symbol
            }
            else
                relocations.WriteInt32((symbol << 8) + 7); // jmp_slot+symbol
            relocations.WriteNumber(0);
            return index++;
        }
    }
}
