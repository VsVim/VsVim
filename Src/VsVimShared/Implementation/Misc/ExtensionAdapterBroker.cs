using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using EditorUtils;

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

        bool IExtensionAdapter.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            foreach (var extensionAdapter in _extensionAdapters)
            {
                if (extensionAdapter.ShouldKeepSelectionAfterHostCommand(command, argument))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
