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
        /// In order to host the editor we need to provide an ITextUndoHistory export.  However 
        /// we can't simply export it from the DLL because it would conflict with Visual Studio's
        /// export of ITextUndoHistoryRegistry in the default scenario.  This ComposablePartCatalog
        /// is simply here to hand export the type in the hosted scenario only
        /// </summary>
        private sealed class UndoExportProvider : ExportProvider
        {
            private readonly IBasicUndoHistoryRegistry _basicUndoHistoryRegistry;
            private readonly string _textUndoHistoryRegistryContractName;
            private readonly string _basicUndoHistoryRegistryContractName;
            private readonly Export _export;

            internal UndoExportProvider()
            {
                _textUndoHistoryRegistryContractName = AttributedModelServices.GetContractName(typeof(ITextUndoHistoryRegistry));
                _basicUndoHistoryRegistryContractName = AttributedModelServices.GetContractName(typeof(IBasicUndoHistoryRegistry));
                _basicUndoHistoryRegistry = EditorUtilsFactory.CreateBasicUndoHistoryRegistry();
                _export = new Export(_textUndoHistoryRegistryContractName, () => _basicUndoHistoryRegistry);
            }

            protected override IEnumerable<Export> GetExportsCore(ImportDefinition definition, AtomicComposition atomicComposition)
            {
                if (definition.ContractName == _textUndoHistoryRegistryContractName ||
                    definition.ContractName == _basicUndoHistoryRegistryContractName)
                {
                    yield return _export;
                }
            }
        }
    }
}
