using System;
using System.Collections.Generic;
using System.Text;

namespace Compiler
{
    public abstract class Importer
    {
		public abstract void WriteGlobalSymbol(string name, Placeholder location);
        public abstract Placeholder FetchImport(string namespaceName, string className, string fieldName);
        public abstract Placeholder FetchImportAsPointer(string library, string entryPoint);
    }
}
