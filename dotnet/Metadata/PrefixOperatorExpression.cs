using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class PrefixOperatorExpression : Expression
    {
        private Expression parent;
        private TypeReference type;
        private string mnemonic;
        private string name;

        Expression call;

        public PrefixOperatorExpression(ILocation location, Expression parent, string mnemonic, string name)
            : base(location)
        {
            Require.Assigned(parent);
            Require.NotEmpty(mnemonic);
            Require.NotEmpty(name);
            this.parent = parent;
            this.mnemonic = mnemonic;
            this.name = name;
        }

        public override Expression InstantiateTemplate (Dictionary<string, TypeName> parameters)
        {
            return new PrefixOperatorExpression(this, parent.InstantiateTemplate(parameters), mnemonic, name);
        }

        public override void Resolve (Generator generator)
        {
            base.Resolve(generator);
            parent.Resolve(generator);
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            parent.Prepare(generator, null);

            TypeReference parentType = parent.TypeReference;

            string signature = parentType.TypeName.Data + ":"+name;

            if (signature == "pluk.base.Bool:OperatorNot")
                type = parentType;
            else if (signature =="pluk.base.Int:OperatorNegate")
                type = parentType;
            else
            {       
                FieldExpression field = new FieldExpression(this, new Identifier(this, name));
                field.SetParentDoNotGenerate(parent);
                call = new CallExpression(this, field);
                call.Resolve(generator);
                call.Prepare(generator, null);
                type = call.TypeReference;
            }
        }

        public override void Generate (Generator generator)
        {
            base.Generate(generator);
            base.Generate(generator);
            parent.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);


            if (call != null)
            {
                call.Generate(generator);
            }
            else
            {
                TypeReference parentType = parent.TypeReference;
                string signature = parentType.TypeName.Data + ":"+name;
    
                if (signature == "pluk.base.Bool:OperatorNot")
                    generator.Assembler.BooleanNot();
                else if (signature =="pluk.base.Int:OperatorNegate")
                    generator.Assembler.IntegerNegate();
            }
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(type); return type; } }        
    }
}
