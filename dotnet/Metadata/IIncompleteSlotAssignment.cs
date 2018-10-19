using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public interface IIncompleteSlotAssignment
    {
        void AllowRetrieval();
        bool IsIncompleteSlot();
    }
}
