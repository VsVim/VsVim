using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(IExtensionAdapterBroker))]
    internal sealed class ExtensionAdapterBroker : IExtensionAdapterBroker
    {
        private readonly ReadOnlyCollection<Lazy<IExtensionAdapter>> _extensionAdapters;

        [ImportingConstructor]
        internal ExtensionAdapterBroker([ImportMany] IEnumerable<Lazy<IExtensionAdapter>> collection)
        {
            _extensionAdapters = collection.ToReadOnlyCollection();
        }

        IEnumerable<IExtensionAdapter> IExtensionAdapterBroker.ExtensionAdapters
        {
            get { return _extensionAdapters.Select(x => x.Value); }
        }

        private bool? RunOnAll(Func<IExtensionAdapter, bool?> func)
        {
            foreach (var extensionAdapter in _extensionAdapters)
            {
                var result = func(extensionAdapter.Value);
                if (result.HasValue)
                {
                    return result;
                }
            }

            return null;
        }

        bool? IExtensionAdapter.IsUndoRedoExpected
        {
            get { return RunOnAll(e => e.IsUndoRedoExpected); }
        }

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView)
        {
            return RunOnAll(e => e.ShouldCreateVimBuffer(textView));
        }

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView)
        {
            return RunOnAll(e => e.IsIncrementalSearchActive(textView));
        }

        bool? IExtensionAdapter.UseDefaultCaret
        {
            get { return RunOnAll(e => e.UseDefaultCaret); }
        }
    }
}
