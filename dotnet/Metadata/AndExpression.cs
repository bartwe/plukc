using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class AndExpression : Expression
    {
        Expression left;
        Expression right;
        TypeReference type;

        CallExpression call;

        public AndExpression(ILocation location, Expression left, Expression right)
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

        public override void Prepare (Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            left.Prepare(generator, null);
            TypeReference leftType = left.TypeReference;
            
            if (leftType.TypeName.Data == "pluk.base.Bool")
            {
                right.Prepare(generator, leftType);
                type = leftType;
            }
            else
            {
                FieldExpression field = new FieldExpression(this, new Identifier(this, "OperatorAnd"));
                field.SetParentDoNotGenerate(left);
                call = new CallExpression(this, field);
                call.AddParameter(right);
                call.Resolve(generator);
                call.Prepare(generator, inferredType);
                type = call.TypeReference;
            }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            left.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);

            TypeReference leftType = left.TypeReference;
            if (call == null)
            {
                JumpToken skip = generator.Assembler.CreateJumpToken();
                generator.Assembler.JumpIfFalse(skip);
                right.Generate(generator);
                leftType.GenerateConversion(this, generator, right.TypeReference);
                generator.Assembler.SetDestination(skip);
            }
            else
            {
                call.Generate(generator);
            }
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(type); return type; } }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new AndExpression(this, left.InstantiateTemplate(parameters), right.InstantiateTemplate(parameters));
        }
    }
}
