using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public class JumpToken
    {
        private JumpTokenKind kind;
        private List<IntToken> jumpSite32 = new List<IntToken>();
        private List<LongToken> jumpSite64 = new List<LongToken>();
        private Placeholder destination;
        private bool destinationSet;
        
        public bool DestinationSet { get { return destinationSet; } }
        public int JumpCount { get { return jumpSite32.Count + jumpSite64.Count; } }

        public void SetKind(JumpTokenKind kind)
        {
            this.kind = kind;
        }

        public void SetDestination(Placeholder location)
        {
            if (destinationSet)
                throw new InvalidOperationException();
            destination = location;
            destinationSet = true;
            if ((jumpSite32 != null) || (jumpSite64 != null))
                Complete();
        }

        public void SetJumpSite(IntToken jumpSite)
        {
            if (jumpSite == null)
                throw new ArgumentNullException("jumpSite");
            this.jumpSite32.Add(jumpSite);
            if (destinationSet)
                Complete();
        }

        public void SetJumpSite(LongToken jumpSite)
        {
            if (jumpSite == null)
                throw new ArgumentNullException("jumpSite");
            this.jumpSite64.Add(jumpSite);
            if (destinationSet)
                Complete();
        }

        public void Complete()
        {
            if (kind == JumpTokenKind.Absolute)
            {
                Require.Implementation("Cannot complete an absolute jumptoken.");
            }
            else
            {
                foreach (IntToken entry in jumpSite32)
                    entry.SetValue((int)destination.MemoryDistanceFrom(entry.Location.Increment(4)));
                foreach (LongToken entry in jumpSite64)
                    entry.SetValue(destination.MemoryDistanceFrom(entry.Location.Increment(8)));
            }
        }
    }

    public enum JumpTokenKind { Relative, Absolute };
}