using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.Mac
{
    [Export(typeof(IDisplayWindowBrokerFactoryService))]
    internal sealed class DisplayWindowBrokerFactoryService : IDisplayWindowBrokerFactoryService
    {
        public IDisplayWindowBroker GetDisplayWindowBroker(ITextView textView)
        {
            return new DisplayWindowBroker(textView);
        }
    }

    internal sealed class DisplayWindowBroker : IDisplayWindowBroker
    {
        private readonly ITextView _textView;

        public DisplayWindowBroker(ITextView textView)
        {
            _textView = textView;
        }
        public ITextView TextView => _textView;

        public bool IsCompletionActive => false;

        public bool IsSignatureHelpActive => false;

        public bool IsQuickInfoActive => false;

        public void DismissDisplayWindows()
        {
            throw new NotImplementedException();
        }
    }
}
