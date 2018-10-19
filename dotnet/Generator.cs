using System;
using System.Collections.Generic;
using System.Text;
using Compiler.Metadata;

namespace Compiler
{
    public abstract class Generator : IDisposable
    {
        private Resolver resolver;
        private DefinitionCollection store;

        private Assembler assembler;
        private Placeholder allocator;
        private Placeholder toucher;
        private Placeholder disposer;
        private Placeholder exit;
        private Placeholder setup;
        private Placeholder saveStackRoot;
        protected Placeholder callStack;
        private Region overflowExceptionRegion;

        public abstract Importer Importer { get; }
        public abstract Symbols Symbols { get; }
        public abstract Region Statics { get; }

        public Resolver Resolver { get { return resolver; } }
        public Placeholder Allocator { get { return allocator; } }
        public Placeholder Toucher { get { return toucher; } }
        public Placeholder Disposer { get { return disposer; } }
        public Placeholder Exit { get { return exit; } }
        public Placeholder Setup { get { return setup; } }
        public Placeholder SaveStackRoot { get { return saveStackRoot; } }
        public Placeholder CallStackData { get { return callStack; } }
        public Placeholder OverflowException { get { return overflowExceptionRegion.BaseLocation; } }
        public Region OverflowExceptionRegion { get { return overflowExceptionRegion; } }

        public IEnumerable<Definition> Definitions { get { return store.Definitions; } }

        protected Generator(IEnumerable<string> paths, bool ignoreFileCase)
        {
            store = new DefinitionCollection();
            resolver = new Resolver(store, paths, ignoreFileCase);
        }

        protected void SetupExceptions()
        {
            overflowExceptionRegion = AllocateDataRegion();
        }

        public abstract void SetModuleName(string moduleName);

        public Assembler Assembler
        {
            get
            {
                Require.Assigned(assembler);
                return assembler;
            }
            set
            {
                Require.Assigned(value);
                assembler = value; ;
            }
        }

        public void AllocateAssembler()
        {
            assembler = InnerAllocateAssembler();
        }

        public abstract Region AllocateDataRegion();

        public abstract void WriteToFile(Region entryPoint);

        public abstract Placeholder AddTextLengthPrefix(string text);

        public abstract void AddCallTraceEntry(Placeholder retPointer, ILocation location, string definition, string method);

        protected abstract Assembler InnerAllocateAssembler();

        protected void SetExternals(Placeholder allocator, Placeholder toucher, Placeholder disposer, Placeholder exit, Placeholder setup, Placeholder saveStackRoot)
        {
            this.allocator = allocator;
            this.toucher = toucher;
            this.disposer = disposer;
            this.exit = exit;
            this.setup = setup;
            this.saveStackRoot = saveStackRoot;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (resolver != null)
                {
                    resolver.Dispose();
                    resolver = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void CheckOverflow(ILocation location)
        {
            Placeholder retsite = Assembler.CheckOverflow(OverflowException); 
            if (Resolver.CurrentDefinition != null)
                AddCallTraceEntry(retsite, location, Resolver.CurrentDefinition.Name.DataModifierLess, Resolver.CurrentFieldName);
            else
                AddCallTraceEntry(retsite, location, "", Resolver.CurrentFieldName);
        }
    }
}
