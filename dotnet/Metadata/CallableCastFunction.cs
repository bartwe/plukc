using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class CallableCastFunction
    {
        private ILocation location;
        private Placeholder functionPointer;

        public Placeholder FunctionPointer { get { Require.Assigned(functionPointer); return functionPointer; } }

        public CallableCastFunction(ILocation location)
        {
            Require.Assigned(location);
            this.location = location;
        }

        public void Generate(Generator generator)
        {
            generator.AllocateAssembler();
            functionPointer = generator.Assembler.Region.BaseLocation;
            Assembler a = generator.Assembler;
            generator.Symbols.Source(a.Region.BaseLocation, location);
            a.StartFunction();
            //TODO: implementation ?
            a.Empty();
            // the current type supports nothing, not even its own type...
            a.StopFunction();
            generator.Symbols.Source(a.Region.CurrentLocation, location, SourceMark.EndSequence);
            generator.Symbols.WriteData(a.Region.BaseLocation, a.Region.Length, "ccf");
        }

    }
}
