using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class LambdaExpression : Expression
    {
        List<Identifier> parameters;
        List<TypeName> parameterTypeNames;
        List<TypeReference> parameterTypes;
        TypeReference returnType;
        Statement statement;
        Placeholder functionPointer;

        public LambdaExpression(ILocation location, IEnumerable<Identifier> parameters, Statement statement)
            : base(location)
        {
            Require.Assigned(parameters);
            Require.Assigned(statement);
            this.parameters = new List<Identifier>();
            parameterTypes = new List<TypeReference>();
            foreach (Identifier p in parameters)
            {
                this.parameters.Add(p);
                parameterTypes.Add(null);
            }
            this.statement = statement;
        }

        public LambdaExpression(ILocation location, IEnumerable<Identifier> parameters, IEnumerable<TypeName> types, Statement statement)
            : base(location)
        {
            Require.Assigned(parameters);
            Require.Assigned(statement);
            this.parameters = new List<Identifier>();
            parameterTypeNames = new List<TypeName>();
            foreach (Identifier p in parameters)
                this.parameters.Add(p);
            foreach (TypeName type in types)
                parameterTypeNames.Add(type);
            this.statement = statement;
        }

        private LambdaExpression(ILocation location, List<Identifier> parameters, List<TypeName> types, Statement statement)
            : base(location)
        {
            Require.Assigned(parameters);
            Require.Assigned(statement);
            this.parameters = parameters;
            parameterTypeNames = types;
            this.statement = statement;
        }

        public override Expression InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            if (parameterTypeNames == null)
                return new LambdaExpression(this, this.parameters, statement.InstantiateTemplate(parameters));
            else
            {
                List<TypeName> pttt = new List<TypeName>();
                foreach (TypeName t in parameterTypeNames)
                    pttt.Add(t.InstantiateTemplate(parameters));
                return new LambdaExpression(this, this.parameters, pttt, statement.InstantiateTemplate(parameters));
            }
        }

        public override void Resolve(Generator generator)
        {
            base.Resolve(generator);
            if (parameterTypeNames != null)
            {
                parameterTypes = new List<TypeReference>();
                foreach (TypeName name in parameterTypeNames)
                    parameterTypes.Add(generator.Resolver.ResolveType(name, name));
            }
            statement.Resolve(generator);
        }

        protected override bool InnerNeedsInference(Generator generator, TypeReference inferredHint)
        {
            return true;
        }

        public override void Prepare(Generator generator, TypeReference inferredType)
        {
            base.Prepare(generator, inferredType);
            FunctionTypeReference ftr = null;
            if (inferredType is FunctionTypeReference)
                ftr = (FunctionTypeReference)inferredType;

            if (ftr != null)
            {
                returnType = ftr.ReturnType;
                for (int i = 0; (i < ftr.FunctionParameters.Count) && (i < parameters.Count); ++i)
                    if (parameterTypes[i] == null)
                        parameterTypes[i] = ftr.FunctionParameters[i];
            }
            for (int i = 0; i < parameterTypes.Count; ++i)
            {
                if (parameterTypes[i] == null)
                    throw new CompilerException(this, Resource.TypeOfExpressionUnclear);
            }
            if (returnType == null)
                throw new CompilerException(this, Resource.TypeOfExpressionUnclear);
        }

        void GenerateLambda(Generator generator)
        {
            Assembler oldAssembler = generator.Assembler;
            offset = oldAssembler.SlotCount();
            generator.AllocateAssembler();
            Assembler inner = generator.Assembler;
            generator.Assembler = new LambdaAssembler(this, generator.Assembler);
            generator.Resolver.EnterContextParentReadOnly();
            closureSlot = offset;
            localSlots.Add(closureSlot, inner.AddParameter()); // closure, or nothing if no fields
            Parameters p = new Parameters();
            for (int i = 0; i < parameters.Count; ++i)
            {
                int s = inner.AddParameter();
                int ex = offset + localSlots.Count;
                localSlots.Add(ex, s);
                generator.Resolver.AddVariable(parameters[i], parameterTypes[i], ex, true);
                generator.Resolver.AssignSlot(ex);
                generator.Resolver.RetrieveSlot(this, ex, false); // todo: make warning: treat function arguments as used
                ParameterMetadata pm = p.AddParameter(parameters[i], parameterTypes[i], parameters[i]);
                pm.Bind(ex);
            }
            generator.Resolver.SetContextParameters(p);

            statement.Prepare(generator);
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
            generator.Assembler.StartFunction();
            JumpToken returnToken = generator.Assembler.CreateJumpToken();
            generator.Resolver.RegisterGoto("@return", returnToken);
            JumpToken recurToken = generator.Assembler.CreateJumpToken();
            generator.Assembler.SetDestination(recurToken);
            generator.Resolver.RegisterGoto("@recur", recurToken);
            statement.Generate(generator, returnType);
            generator.Assembler.SetDestination(returnToken);
            generator.Assembler.StopFunction();
            generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
            generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "lambda:");

            functionPointer = generator.Assembler.Region.BaseLocation;

            generator.Resolver.LeaveContext();
            generator.Assembler = oldAssembler;
        }

        public override void Generate(Generator generator)
        {
            base.Generate(generator);
            GenerateLambda(generator);

            Region runtimeStruct = generator.AllocateDataRegion();
            if (closureSlots.Count > 0)
            {
                generator.Assembler.CallAllocator(generator.Allocator, closureSlots.Count, runtimeStruct.BaseLocation);
                runtimeStruct.WriteNumber(1); //gc
            }
            else
            {
                generator.Assembler.SetImmediateValue(runtimeStruct.BaseLocation, 0);
                runtimeStruct.WriteNumber(0);
            }
            runtimeStruct.WriteNumber(0); //todo:cast function
            runtimeStruct.WriteNumber(0);
            runtimeStruct.WriteNumber(0);
            runtimeStruct.WritePlaceholder(functionPointer);
            runtimeStruct.WritePlaceholder(runtimeStruct.BaseLocation);
            int i = 0;
            foreach (KeyValuePair<int, int> kv in closureSlots)
            {
                //note: rs entry is nt the one matching with the closureSlots here, which are basicy in random order.
                runtimeStruct.WriteNumberTimesBitness(2 * (i++));
                //todo: crappy assembly
                generator.Assembler.PushValue();
                generator.Assembler.PushValue();
                generator.Assembler.RetrieveVariable(kv.Key);
                generator.Assembler.StoreInFieldOfSlot(generator.Toucher, kv.Value);
                generator.Assembler.PopValue();
            }
        }

        public override TypeReference TypeReference
        {
            get
            {
                return new FunctionTypeReference(this, returnType, parameterTypes);
            }
        }

        //wheh, this reeks, refactor to own class
        private Dictionary<int, int> localSlots = new Dictionary<int, int>();
        private Dictionary<int, int> closureSlots = new Dictionary<int, int>();
        private int offset;
        private int closureSlot;

        public int AddVariable(int slot)
        {
            int idx = offset + localSlots.Count;
            localSlots.Add(idx, slot);
            return idx;
        }

        public int SlotCount()
        {
            return offset + localSlots.Count;
        }

        public bool IsClosureSlot(int slot)
        {
            return !localSlots.ContainsKey(slot);
        }

        public int LocalSlot(int slot)
        {
            return localSlots[slot];
        }

        public int ClosureField(int slot)
        {
            int r;
            if (closureSlots.TryGetValue(slot, out r))
                return r;
            r = closureSlots.Count + 6;
            closureSlots.Add(slot, r);
            return r;
        }

        public int ClosureSlot { get { return closureSlot; } }
    }
}
