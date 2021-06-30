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
using Microsoft.VisualStudio.Threading;
using System.Threading;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;

namespace Vim.EditorHost
{
    public sealed partial class VimEditorHostFactory
    {
        /// <summary>
        /// Beginning in 15.0 the editor took a dependency on JoinableTaskContext.  Need to provide that 
        /// export here. 
        /// </summary>
        private sealed class JoinableTaskContextExportProvider : ExportProvider
        {
            internal static string TypeFullName => typeof(JoinableTaskContext).FullName;
            private readonly Export _export;
            private readonly JoinableTaskContext _context;

            internal JoinableTaskContextExportProvider()
            {
                _export = new Export(TypeFullName, GetValue);
#pragma warning disable VSSDK005
                _context = new JoinableTaskContext(Thread.CurrentThread, new DispatcherSynchronizationContext());
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
            {
                if (definition.ContractName == TypeFullName)
                { 
                    yield return _export;
                }
            }

            private object GetValue() => _context;
        }
    }
}