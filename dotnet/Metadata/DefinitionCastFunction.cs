using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class DefinitionCastFunction
    {
        private Definition definition;
        private Placeholder functionPointer;

        public Placeholder FunctionPointer { get { Require.Assigned(functionPointer); return functionPointer; } }

        public DefinitionCastFunction(Definition definition)
        {
            Require.Assigned(definition);
            this.definition = definition;
        }

        public void Generate(Generator generator)
        {
            generator.AllocateAssembler();
            functionPointer = generator.Assembler.Region.BaseLocation;
            Assembler a = generator.Assembler;
            int thisidx = a.AddParameter(); // this;
            int typeidx = a.AddParameter(); // type
            generator.Symbols.Source(a.Region.CurrentLocation, definition);
            a.StartFunction();
            //TODO: a binary searchtree would be fun
            foreach (KeyValuePair<int, DefinitionTypeReference> titrkvp in definition.GetSupportedTypesMap())
            {
                a.RetrieveVariable(typeidx);
                a.PushValue();
                a.SetOnlyValue(titrkvp.Value.Id);
                a.IntegerEquals();
                JumpToken noMatch = a.CreateJumpToken();
                a.JumpIfFalse(noMatch);
                a.RetrieveVariable(thisidx);
                a.SetTypePart(definition.RuntimeStruct);
                if (titrkvp.Key != -1)
                    a.TypeConversionNotNull(titrkvp.Key);
                a.StopFunction();
                a.SetDestination(noMatch);
            }
            a.Empty();
            a.StopFunction();
            generator.Symbols.Source(a.Region.CurrentLocation, definition, SourceMark.EndSequence);
            generator.Symbols.WriteData(a.Region.BaseLocation, a.Region.Length, "dcf:" + definition.Name.Data);
        }
    }
}
