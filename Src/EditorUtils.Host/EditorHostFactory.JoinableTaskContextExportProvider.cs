using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.Win32;

namespace EditorUtils
{
    public sealed partial class EditorHostFactory
    {
        /// <summary>
        /// Beginning in 15.0 the editor took a dependency on JoinableTaskContext.  Need to provide that 
        /// export here. 
        /// </summary>
        private sealed class JoinableTaskContextExportProvider : ExportProvider
        {
            internal const string AssemblyName = "Microsoft.VisualStudio.Threading";
            internal const string TypeShortName = "JoinableTaskContext";
            internal const string TypeFullName = AssemblyName + "." + TypeShortName;
            private readonly Export _export;
            private object _instance;

            internal JoinableTaskContextExportProvider()
            {
                _export = new Export(TypeFullName, GetValue);
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
            {
                if (definition.ContractName == TypeFullName)
                { 
                    yield return _export;
                }
            }

            private object GetValue()
            {
                if (_instance == null)
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == AssemblyName);
                    var type = assembly.GetType(TypeFullName);
                    var ctor = type.GetConstructor(new Type[0] { });
                    _instance = ctor.Invoke(null);
                }

                return _instance;
            }
        }
    }
}
