using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler.Metadata
{
    public class Property : Callable
    {
        private Modifiers getModifiers;
        private Modifiers setModifiers;
        private TypeName typeName;
        private Identifier name;
        private Statement getStatement;
        private Statement setStatement;
        private Placeholder getStatementPointer;
        private Placeholder setStatementPointer;
        private TypeReference type;
        private Parameters getParameters;
        private Parameters setParameters;

        public Identifier Name { get { return name; } }
        public Modifiers GetModifiers { get { return getModifiers; } }
        public Modifiers SetModifiers { get { return setModifiers; } }

        public Property(ILocation location, Modifiers getModifiers, Modifiers setModifiers, TypeName typeName, Identifier name, Statement getStatement, Statement setStatement)
            : base(location)
        {
            Require.Assigned(getModifiers);
            Require.Assigned(setModifiers);
            Require.Assigned(typeName);
            Require.Assigned(name);
            this.getModifiers = getModifiers;
            this.setModifiers = setModifiers;
            this.typeName = typeName;
            this.name = name;
            this.getStatement = getStatement;
            this.setStatement = setStatement;
            getParameters = new Parameters();
            setParameters = new Parameters();
            setParameters.AddParameter(location, typeName, new Identifier(location, "value"));
        }

        public override TypeReference ReturnType
        {
            get { return type; }
        }

        public override Parameters Parameters
        {
            get { throw new NotImplementedException(); }
        }

        public override TypeReference AsTypeReference()
        {
            throw new NotImplementedException();
        }

        public override void Resolve(Generator generator)
        {
            type = generator.Resolver.ResolveType(this, typeName);
            if (getModifiers.Static)
                getParameters.AddThisParameter(this, new StaticTypeReference(this, ParentDefinition.TypeReference));
            else
                getParameters.AddThisParameter(this, ParentDefinition.TypeReference);
            getParameters.Resolve(generator);
            if (setModifiers.Static)
                setParameters.AddThisParameter(this, new StaticTypeReference(this, ParentDefinition.TypeReference));
            else
                setParameters.AddThisParameter(this, ParentDefinition.TypeReference);
            setParameters.Resolve(generator);
            if (getStatement != null)
                getStatement.Resolve(generator);
            if (setStatement != null)
                setStatement.Resolve(generator);
        }

        public override void Prepare(Generator generator, Set<TypeReference> dependsUpon)
        {
            dependsUpon.Put(type);
        }

        public override void Generate(Generator generator)
        {
            if (getStatement != null)
            {
                generator.Resolver.EnterContext();
                generator.Resolver.SetContextParameters(getParameters);
                generator.Resolver.CurrentFieldName = name.Data + ":get";
                generator.AllocateAssembler();
                getParameters.Generate(generator);
                ParameterMetadata thisParam = getParameters.Find("this");
                generator.Resolver.SetImplicitFields(thisParam.Slot, thisParam.TypeReference);
                getStatement.Prepare(generator);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Assembler.StartFunction();
                JumpToken returnToken = generator.Assembler.CreateJumpToken();
                generator.Resolver.RegisterGoto("@return", returnToken);
                JumpToken recurToken = generator.Assembler.CreateJumpToken();
                generator.Assembler.SetDestination(recurToken);
                generator.Resolver.RegisterGoto("@recur", recurToken);
                getStatement.Generate(generator, type);
                if (!getStatement.Returns())
                    throw new CompilerException(getStatement, string.Format(Resource.Culture, Resource.NotAllCodePathsReturnAValue));
                generator.Assembler.SetDestination(returnToken);
                generator.Assembler.StopFunction();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "getter:" + ParentDefinition.Name.Data + "." + name.Data);
                getStatementPointer = generator.Assembler.Region.BaseLocation;
                generator.Resolver.LeaveContext();
            }
            if (setStatement != null)
            {
                generator.Resolver.EnterContext();
                generator.Resolver.SetContextParameters(setParameters);
                generator.Resolver.CurrentFieldName = name.Data + ":set";
                generator.AllocateAssembler();
                setParameters.Generate(generator);
                ParameterMetadata thisParam = setParameters.Find("this");
                generator.Resolver.SetImplicitFields(thisParam.Slot, thisParam.TypeReference);
                setStatement.Prepare(generator);
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this);
                generator.Assembler.StartFunction();
                JumpToken returnToken = generator.Assembler.CreateJumpToken();
                generator.Resolver.RegisterGoto("@return", returnToken);
                JumpToken recurToken = generator.Assembler.CreateJumpToken();
                generator.Assembler.SetDestination(recurToken);
                generator.Resolver.RegisterGoto("@recur", recurToken);
                setStatement.Generate(generator, null);
                generator.Assembler.SetDestination(returnToken);
                generator.Assembler.StopFunction();
                generator.Symbols.Source(generator.Assembler.Region.CurrentLocation, this, SourceMark.EndSequence);
                generator.Symbols.WriteCode(generator.Assembler.Region.BaseLocation, generator.Assembler.Region.Length, "setter:" + ParentDefinition.Name.Data + "." + name.Data);
                setStatementPointer = generator.Assembler.Region.BaseLocation;
                generator.Resolver.LeaveContext();
            }
        }

        public Property InstantiateTemplate(Dictionary<string, TypeName> parameters)
        {
            Statement getStatement = this.getStatement;
            if (getStatement != null)
                getStatement = getStatement.InstantiateTemplate(parameters);
            Statement setStatement = this.setStatement;
            if (setStatement != null)
                setStatement = setStatement.InstantiateTemplate(parameters);
            return new Property(this, getModifiers, setModifiers, typeName.InstantiateTemplate(parameters), name
                , getStatement, setStatement);
        }

        public Placeholder MakeGetPropertyStruct(Generator generator, Definition definition, Placeholder definitionRuntimeStruct)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            Require.Assigned(definitionRuntimeStruct);
            Region propertyStruct = generator.AllocateDataRegion();
            if (definition.GarbageCollectable)
                propertyStruct.WriteNumber(1);
            else
                propertyStruct.WriteNumber(0);
            propertyStruct.WriteNumber(0); // no function cast adaptor for now
            propertyStruct.WriteNumber(0);
            propertyStruct.WriteNumber(0);
            if (!getStatementPointer.IsNull)
                propertyStruct.WritePlaceholder(getStatementPointer);
            else
                propertyStruct.WriteNumber(0);
            propertyStruct.WritePlaceholder(definitionRuntimeStruct);
            generator.Symbols.WriteData(propertyStruct.BaseLocation, propertyStruct.Length, "ps:" + definition.Name.Data + ":" + ParentDefinition.Name.Data + "." + name.Data + ".+get");
            return propertyStruct.BaseLocation;
        }

        public Placeholder MakeSetPropertyStruct(Generator generator, Definition definition, Placeholder definitionRuntimeStruct)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");
            Require.Assigned(definitionRuntimeStruct);
            Region propertyStruct = generator.AllocateDataRegion();
            if (definition.GarbageCollectable)
                propertyStruct.WriteNumber(1);
            else
                propertyStruct.WriteNumber(0);
            propertyStruct.WriteNumber(0); // no function cast adaptor for now
            propertyStruct.WriteNumber(0);
            propertyStruct.WriteNumber(0);
            if (!setStatementPointer.IsNull)
                propertyStruct.WritePlaceholder(setStatementPointer);
            else
                propertyStruct.WriteNumber(0);
            propertyStruct.WritePlaceholder(definitionRuntimeStruct);
            generator.Symbols.WriteData(propertyStruct.BaseLocation, propertyStruct.Length, "ps:" + definition.Name.Data + ":" + ParentDefinition.Name.Data + "." + name.Data + ".+set");
            return propertyStruct.BaseLocation;
        }
    }
}
