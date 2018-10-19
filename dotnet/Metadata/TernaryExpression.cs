using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class TernaryExpression : PostfixExpression
    {
        Expression condition;
        Expression left;
        Expression right;

        TypeReference boolType;

        TypeReference resultType;

        public TernaryExpression(ILocation location, Expression condition, Expression left, Expression right)
            : base(location)
        {
            Require.Assigned(condition);
            Require.Assigned(left);
            Require.Assigned(right);
            this.condition = condition;
            this.left = left;
            this.right = right;
        }

        public TernaryExpression(ILocation location, Expression left, Expression right)
            : base(location)
        {
            Require.Assigned(left);
            Require.Assigned(right);
            this.left = left;
            this.right = right;
        }

        public override void SetParent(Expression parent)
        {
            Require.Assigned(parent);
            Require.Unassigned(condition);
            condition = parent;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new TernaryExpression(this, condition.InstantiateTemplate(parameters), left.InstantiateTemplate(parameters), right.InstantiateTemplate(parameters));
        }

        public override TypeReference TypeReference
        {
            get { Require.Assigned(resultType); return resultType; }
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            condition.Resolve(generator);
            left.Resolve(generator);
            right.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            condition.Prepare(generator, boolType);
            resultType = inferredType;
            left.Prepare(generator, inferredType);
            if (resultType == null)
                resultType = left.TypeReference;
            right.Prepare(generator, inferredType);
        }

        public override bool HasSideEffects()
        {
            return condition.HasSideEffects() || left.HasSideEffects() || right.HasSideEffects();
        }

        public override bool NeedsToBeStored()
        {
            return true;
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            condition.Generate(generator); // boolean
            boolType.GenerateConversion(this, generator, condition.TypeReference);
            JumpToken elseToken = generator.Assembler.CreateJumpToken();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.JumpIfFalse(elseToken);
            left.Generate(generator);
            resultType.GenerateConversion(this, generator, left.TypeReference);
            JumpToken skipElseToken = generator.Assembler.CreateJumpToken();
            generator.Assembler.Jump(skipElseToken);
            generator.Assembler.SetDestination(elseToken);
            right.Generate(generator);
            resultType.GenerateConversion(this, generator, right.TypeReference);
            generator.Assembler.SetDestination(skipElseToken);
        }
    }
}
