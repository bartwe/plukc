using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class IfAssignedExpression : Expression
    {
        Expression left;
        Expression right;
        TypeReference type;

        public IfAssignedExpression(ILocation location, Expression left, Expression right)
            : base(location)
        {
            this.left = left;
            this.right = right;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            left.Resolve(generator);
            right.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return left.NeedsInference(generator, inferredHint);
        }

        public override void Prepare (Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            left.Prepare(generator, inferredType);
            right.Prepare(generator, left.TypeReference);
            type = left.TypeReference;
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            left.Generate(generator);
            JumpToken skip = generator.Assembler.CreateJumpToken();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.JumpIfAssigned(skip);
            right.Generate(generator);
            right.TypeReference.GenerateConversion(this, generator, left.TypeReference);
            generator.Assembler.SetDestination(skip);
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(type); return type; } }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new IfAssignedExpression(this, left.InstantiateTemplate(parameters), right.InstantiateTemplate(parameters));
        }
    }
}
