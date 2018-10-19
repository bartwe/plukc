using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class ThrowStatement : Statement
    {
        private Expression expression;
        private DefinitionTypeReference exceptionType;

        public ThrowStatement(ILocation location, Expression expression)
            : base(location)
        {
            if (expression == null)
                throw new ArgumentNullException("expression");
            this.expression = expression;
        }

        public override Statement InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new ThrowStatement(this, expression.InstantiateTemplate(parameters));
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            expression.Resolve(generator);
            exceptionType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Exception")));
        }

        public override void Generate(Generator generator, TypeReference returnType)
        {
            base.Generate(generator, returnType);
            expression.Prepare(generator, null);
            expression.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            exceptionType.GenerateConversion(this, generator, expression.TypeReference);
            generator.Assembler.PushValue();
            Method m = exceptionType.Definition.FindMethod(new Identifier(this, "Throw"), false, null, null, true);
            int offset = exceptionType.Definition.GetMethodOffset(this, m, exceptionType.Definition); // bypasses visibility
            generator.Assembler.FetchMethod(offset);
            generator.Assembler.PushValue();
            Placeholder retSite = generator.Assembler.CallFromStack(0);
            if (generator.Resolver.CurrentDefinition != null)
                generator.AddCallTraceEntry(retSite, this, generator.Resolver.CurrentDefinition.Name.DataModifierLess, generator.Resolver.CurrentFieldName);
            else
                generator.AddCallTraceEntry(retSite, this, "meh", "raiseOverflow");
            generator.Assembler.PopValue();
            generator.Assembler.ExceptionHandlerInvoke();
        }

        public override bool Returns()
        {
            return true;
        }
    }
}
