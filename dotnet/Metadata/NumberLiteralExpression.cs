using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Compiler.Metadata
{
    class NumberLiteralExpression : Expression
    {
        private bool float_;
        private float floatVal;
        private int value;
        private DefinitionTypeReference byteType;
        private DefinitionTypeReference intType;
        private DefinitionTypeReference floatType;
        private DefinitionTypeReference typeReference;

        private NumberLiteralExpression(NumberLiteralExpression self)
            : base(self)
        {
            value = self.value;
        }

        public NumberLiteralExpression(ILocation location, ParserToken token)
            : base(location)
        {
            if (token == null)
                throw new ArgumentNullException("token");
            string literal = token.Token;
            bool binary = token.Token.EndsWith("#");
            if (binary)
                literal = literal.Substring(0, literal.Length - 1);
            if (!binary)
                float_ = token.Token.Contains(".") || token.Token.Contains("e") || token.Token.Contains("E");
            else
                float_ = false;

            if (float_)
            {
                double val;
                try
                {
                    val = double.Parse(literal, CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new CompilerException(location, "Failed to parse number literal as double. " + literal);
                }

                if ((val < float.MinValue) || (val > float.MaxValue))
                    throw new CompilerException(location, "Number literal out of range for float. " + literal);
                floatVal = (float)val;
            }
            else
            {
                long val;
                try
                {
                    val = long.Parse(literal, CultureInfo.InvariantCulture);
                }
                catch
                {
                    throw new CompilerException(location, "Failed to parse number literal as integer. " + literal);
                }

                if (binary)
                {
                    if ((val > int.MaxValue) && (val <= uint.MaxValue))
                        val = -1 + (val - uint.MaxValue);
                }
                if ((val < int.MinValue) || (val > int.MaxValue))
                    throw new CompilerException(location, "Number literal out of range for integer. " + literal);
                value = (int)val;
            }
        }

        public NumberLiteralExpression(ILocation location, int value)
            : base(location)
        {
            this.value = value;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            return new NumberLiteralExpression(this);
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            intType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Int")));
            floatType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Float")));
            byteType = generator.Resolver.ResolveDefinitionType(this, new TypeName(new Identifier(this, "pluk.base.Byte")));
        }

        public override bool NeedsToBeStored()
        {
            return true;
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return true;
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            if (float_)
                typeReference = floatType;
            else
            {
                if ((inferredType != null) && byteType.Supports(inferredType) && (value >= byte.MinValue) && (value <= byte.MaxValue))
                    typeReference = byteType;
                else if ((inferredType != null) && floatType.Supports(inferredType))
                {
                    float_ = true;
                    typeReference = floatType;
                    floatVal = value;
                }
                else
                    typeReference = intType;
            }
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            if (float_)
            {
                long v;
                if (generator.Assembler.Region.Is64Bit)
                    v = BitConverter.ToInt64(BitConverter.GetBytes((double)floatVal), 0);
                else
                    v = BitConverter.ToInt32(BitConverter.GetBytes(floatVal), 0);
                generator.Assembler.SetImmediateValue(typeReference.RuntimeStruct, v);
            }
            else
                generator.Assembler.SetImmediateValue(typeReference.RuntimeStruct, value);
        }

        public override TypeReference TypeReference
        { get { Require.Assigned(typeReference); return typeReference; } }
    }
}
