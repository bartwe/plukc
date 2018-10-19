using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;

namespace Compiler
{
    class CheckHelper
    {
        public static void SetupExceptionHandlers(Generator generator)
        {
            ILocation nl = new NowhereLocation();
            generator.Resolver.CurrentFieldName = "raiseOverflow";
            generator.AllocateAssembler();
            generator.Assembler.StartFunction();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, nl);
            generator.OverflowExceptionRegion.WritePlaceholder(generator.Assembler.Region.BaseLocation);
            generator.Resolver.EnterDefinitionContext(null);
            Statement s = new ThrowStatement(nl, new CallExpression(nl, new NewExpression(nl, new TypeName(new Identifier(nl, "pluk.base.OverflowException")))));
            s.Resolve(generator);
            s.Prepare(generator);
            s.Generate(generator, null);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, nl, SourceMark.EndSequence);
            generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "raiseOverflow");
            generator.Resolver.LeaveContext();
        }
    }
}
