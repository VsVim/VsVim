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
            throw new NotImplementedException();
        }
    }
}
