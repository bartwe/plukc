using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class InfixOperatorExpression : Expression
    {
        private Expression parent;
        private Expression argument;
        private TypeReference type;
        private string mnemonic;
        private string name;
        private DefinitionTypeReference boolType;

        CallExpression call;

        public InfixOperatorExpression(ILocation location, Expression parent, Expression argument, string mnemonic, string name)
            : base(location)
        {
            Require.Assigned(parent);
            Require.Assigned(argument);
            Require.NotEmpty(mnemonic);
            Require.NotEmpty(name);
            this.parent = parent;
            this.argument = argument;
            this.mnemonic = mnemonic;
            this.name = name;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new InfixOperatorExpression(this, parent.InstantiateTemplate(parameters), argument.InstantiateTemplate(parameters), mnemonic, name);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
            argument.Resolve(generator);
            boolType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Bool")));
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            parent.Prepare(generator, null);

            TypeReference parentType = parent.TypeReference;
//            if (parentType.IsNullable)
//                parentType = ((NullableTypeReference)parentType).Parent;

            FieldExpression field = new FieldExpression(this, new Identifier(this, name));
            field.SetParentDoNotGenerate(parent);
            call = new CallExpression(this, field);
            call.AddParameter(argument);
            call.ParametersPreResolved();
            call.Resolve(generator);
            call.Prepare(generator, null);
            type = call.TypeReference;

            string signature = parentType.TypeName.Data + ":" + name + ":" + argument.TypeReference.TypeName.Data;

            // unassign call if it is build in
            if ((signature == "pluk.base.Int:OperatorEquals:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorNotEquals:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorGreaterThan:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorLessThan:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorGreaterEquals:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorLessEquals:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorAdd:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorSubtract:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorLeft:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorRight:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorMultiply:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorModulo:pluk.base.Int")
              || (signature == "pluk.base.Int:OperatorDivide:pluk.base.Int")

              || (signature == "pluk.base.Bool:OperatorEquals:pluk.base.Bool")
              || (signature == "pluk.base.Bool:OperatorNotEquals:pluk.base.Bool")

              || (signature == "pluk.base.Byte:OperatorEquals:pluk.base.Byte")
              || (signature == "pluk.base.Byte:OperatorNotEquals:pluk.base.Byte")
              || (signature == "pluk.base.Byte:OperatorGreaterThan:pluk.base.Byte")
              || (signature == "pluk.base.Byte:OperatorLessThan:pluk.base.Byte")
              || (signature == "pluk.base.Byte:OperatorGreaterEquals:pluk.base.Byte")
              || (signature == "pluk.base.Byte:OperatorLessEquals:pluk.base.Byte")
              )
                call = null;
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            parent.Generate(generator);

            if (call != null)
                call.Generate(generator);
            else
            {
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                TypeReference parentType = parent.TypeReference;
//                if (parentType.IsNullable)
//                {
//                    generator.Assembler.CrashIfNull();
//                    parentType = ((NullableTypeReference)parentType).Parent;
//                }

                generator.Assembler.PushValue();

                argument.Generate(generator);

                string signature = parentType.TypeName.Data + ":" + name + ":" + argument.TypeReference.TypeName.Data;

                if ((signature == "pluk.base.Int:OperatorEquals:pluk.base.Int")
                    || (signature == "pluk.base.Bool:OperatorEquals:pluk.base.Bool")
                    || (signature == "pluk.base.Byte:OperatorEquals:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerEquals();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if ((signature == "pluk.base.Int:OperatorNotEquals:pluk.base.Int")
                    || (signature == "pluk.base.Bool:OperatorNotEquals:pluk.base.Bool")
                    || (signature == "pluk.base.Byte:OperatorNotEquals:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerNotEquals();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if ((signature == "pluk.base.Int:OperatorGreaterThan:pluk.base.Int")
                    || (signature == "pluk.base.Byte:OperatorGreaterThan:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerGreaterThan();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if ((signature == "pluk.base.Int:OperatorLessThan:pluk.base.Int")
                    || (signature == "pluk.base.Byte:OperatorLessThan:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerLessThan();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if ((signature == "pluk.base.Int:OperatorGreaterEquals:pluk.base.Int")
                    || (signature == "pluk.base.Byte:OperatorGreaterEquals:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerGreaterEquals();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if ((signature == "pluk.base.Int:OperatorLessEquals:pluk.base.Int")
                    || (signature == "pluk.base.Byte:OperatorLessEquals:pluk.base.Byte")
                    )
                {
                    generator.Assembler.IntegerLessEquals();
                    generator.Assembler.SetTypePart(boolType.RuntimeStruct);
                }
                else if (signature == "pluk.base.Int:OperatorAdd:pluk.base.Int")
                {
                    generator.Assembler.IntegerAdd();
                    generator.CheckOverflow(this);
                }
                else if (signature == "pluk.base.Int:OperatorSubtract:pluk.base.Int")
                {
                    generator.Assembler.IntegerSubtract();
                    generator.CheckOverflow(this);
                }
                else if (signature == "pluk.base.Int:OperatorLeft:pluk.base.Int")
                {
                    generator.Assembler.IntegerLeft();
                }
                else if (signature == "pluk.base.Int:OperatorRight:pluk.base.Int")
                {
                    generator.Assembler.IntegerRight();
                }
                else if (signature == "pluk.base.Int:OperatorMultiply:pluk.base.Int")
                {
                    generator.Assembler.IntegerMultiply();
                    generator.CheckOverflow(this);
                }
                else if (signature == "pluk.base.Int:OperatorDivide:pluk.base.Int")
                {
                    generator.Assembler.IntegerDivide();
                }
                else if (signature == "pluk.base.Int:OperatorModulo:pluk.base.Int")
                {
                    generator.Assembler.IntegerModulo();
                }
                else
                    Require.NotCalled();
            }
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(type); return type; } }
    }
}
