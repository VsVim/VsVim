using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Vim;

namespace VsVim
{
    [Export(typeof(ICompletionWindowBroker))]
    internal class CompletionWindowBroker : ICompletionWindowBroker
    {
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureBroker;

        [ImportingConstructor]
        internal CompletionWindowBroker(
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureBroker)
        {
            _completionBroker = completionBroker;
            _signatureBroker = signatureBroker;
        }

        bool ICompletionWindowBroker.IsCompletionWindowActive(Microsoft.VisualStudio.Text.Editor.ITextView view)
        {
            return _completionBroker.IsCompletionActive(view) || _signatureBroker.IsSignatureHelpActive(view);
        }

        void ICompletionWindowBroker.DismissCompletionWindow(Microsoft.VisualStudio.Text.Editor.ITextView view)
        {
            if (_completionBroker.IsCompletionActive(view))
            {
                _completionBroker.DismissAllSessions(view);
            }

            if (_signatureBroker.IsSignatureHelpActive(view))
            {
                _signatureBroker.DismissAllSessions(view);
            }
        }
    }
}
