using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using EditorUtils;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IExtensionAdapterBroker))]
    internal sealed class ExtensionAdapterBroker : IExtensionAdapterBroker
    {
        private readonly ReadOnlyCollection<IExtensionAdapter> _extensionAdapters;

        [ImportingConstructor]
        internal ExtensionAdapterBroker([ImportMany] IEnumerable<IExtensionAdapter> collection)
        {
            _extensionAdapters = collection.ToReadOnlyCollection();
        }

        ReadOnlyCollection<IExtensionAdapter> IExtensionAdapterBroker.ExtensionAdapters
        {
            get { return _extensionAdapters; }
        }

        private bool? RunOnAll(Func<IExtensionAdapter, bool?> func)
        {
            foreach (var extensionAdapter in _extensionAdapters)
            {
                var result = func(extensionAdapter);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }

        bool? IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            return RunOnAll(e => e.ShouldKeepSelectionAfterHostCommand(command, argument));
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            return RunOnAll(e => e.ShouldCreateVimBuffer(textView));
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return RunOnAll(e => e.IsIncrementalSearchActive(textView));
        }
    }
}
