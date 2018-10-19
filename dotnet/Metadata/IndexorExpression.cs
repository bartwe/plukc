using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    class IndexorExpression : PostfixExpression, IAssignableExpression
    {
        private Expression parent;
        private FieldExpression field;
        private CallExpression call;
        private List<Expression> parameters = new List<Expression>();
        private bool setter;

        private DefinitionTypeReference boolType;
        private DefinitionTypeReference byteType;
        private DefinitionTypeReference intType;

        public IndexorExpression(ILocation location)
            : base(location)
        {
            field = new FieldExpression(location, new Identifier(location, "OperatorGetIndex"));
            call = new CallExpression(location, field);
            setter = false;
        }

        private IndexorExpression(ILocation location, Expression parent)
            : base(location)
        {
            Require.Assigned(parent);
            this.parent = parent;
            field = new FieldExpression(location, new Identifier(location, "OperatorSetIndex"));
            field.SetParent(parent);
            call = new CallExpression(location, field);
            setter = true;
        }

        public override void SetParent(Expression expression)
        {
            Require.Unassigned(parent);
            field.SetParent(expression);
            this.parent = expression;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            IndexorExpression result;
            if (setter)
            {
                result = new IndexorExpression(this, parent.InstantiateTemplate(parameters));
            }
            else
            {
                result = new IndexorExpression(this);
                result.SetParent(parent.InstantiateTemplate(parameters));
            }
            foreach (Expression param in this.parameters)
                result.AddParameter(param.InstantiateTemplate(parameters));
            return result;
        }

        public void AddParameter(Expression expression)
        {
            parameters.Add(expression);
            call.AddParameter(expression);
        }

        public Expression ConvertToAssignment(ILocation location, Expression value)
        {
            IndexorExpression result = new IndexorExpression(this, parent);
            foreach (Expression parameter in parameters)
                result.AddParameter(parameter);
            result.AddParameter(value);
            return result;
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            call.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
            byteType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Byte")));
            intType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Int")));
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return call.NeedsInference(generator, inferredHint);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            call.Prepare(generator, inferredType);
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);

            string signature;
            signature = setter ? "set:" : "get:";
            signature += parent.TypeReference.TypeName.Data;
            foreach (Expression param in parameters)
                signature += ":" + param.TypeReference.TypeName.Data;
            if ((signature == "get:pluk.base.Array<pluk.base.Bool>:pluk.base.Int"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.ArrayFetchByte();
                generator.Assembler.SetTypePart(boolType.RuntimeStruct);
            }
            else if ((signature == "set:pluk.base.Array<pluk.base.Bool>:pluk.base.Int:pluk.base.Bool"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.PushValue();
                parameters[1].Generate(generator);
                generator.Assembler.ArrayStoreByte();
            }
            else if ((signature == "get:pluk.base.Array<pluk.base.Byte>:pluk.base.Int"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.ArrayFetchByte();
                generator.Assembler.SetTypePart(byteType.RuntimeStruct);
            }
            else if ((signature == "set:pluk.base.Array<pluk.base.Byte>:pluk.base.Int:pluk.base.Byte"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.PushValue();
                parameters[1].Generate(generator);
                generator.Assembler.ArrayStoreByte();
            }
            else if ((signature == "get:pluk.base.Array<pluk.base.Int>:pluk.base.Int"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.ArrayFetchInt();
                generator.Assembler.SetTypePart(intType.RuntimeStruct);
            }
            else if ((signature == "set:pluk.base.Array<pluk.base.Int>:pluk.base.Int:pluk.base.Int"))
            {
                parent.Generate(generator);
                generator.Assembler.PushValue();
                parameters[0].Generate(generator);
                generator.Assembler.PushValue();
                parameters[1].Generate(generator);
                generator.Assembler.ArrayStoreInt();
            }
            else
                call.Generate(generator);
        }

        public override TypeReference TypeReference { get { return call.TypeReference; } }
    }
}
