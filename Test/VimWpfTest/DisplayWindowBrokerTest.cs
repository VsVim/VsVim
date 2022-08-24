using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UI.Wpf.Implementation.Misc;

#pragma warning disable CS0618 
namespace Vim.UI.Wpf.UnitTest
{
    public class DisplayWindowBrokerTest
    {
        private readonly Mock<ICompletionBroker> _completionBroker;
        private readonly Mock<ISignatureHelpBroker> _signatureBroker;
        private readonly Mock<IAsyncQuickInfoBroker> _quickInfoBroker;
        private readonly Mock<ITextView> _textView;
        private readonly DisplayWindowBroker _brokerRaw;
        private readonly IDisplayWindowBroker _broker;

        public DisplayWindowBrokerTest()
        {
            _completionBroker = new Mock<ICompletionBroker>();
            _signatureBroker = new Mock<ISignatureHelpBroker>();
            _quickInfoBroker = new Mock<IAsyncQuickInfoBroker>();
            _textView = new Mock<ITextView>();
            _brokerRaw = new DisplayWindowBroker(
                _textView.Object,
                _completionBroker.Object,
                _signatureBroker.Object,
                _quickInfoBroker.Object);
            _broker = _brokerRaw;
        }

    }
}
