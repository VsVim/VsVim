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
        private readonly Mock<IQuickInfoBroker> _quickInfoBroker;
        private readonly Mock<ITextView> _textView;
        private readonly DisplayWindowBroker _brokerRaw;
        private readonly IDisplayWindowBroker _broker;

        public DisplayWindowBrokerTest()
        {
            _completionBroker = new Mock<ICompletionBroker>();
            _signatureBroker = new Mock<ISignatureHelpBroker>();
            _quickInfoBroker = new Mock<IQuickInfoBroker>();
            _textView = new Mock<ITextView>();
            _brokerRaw = new DisplayWindowBroker(
                _textView.Object,
                _completionBroker.Object,
                _signatureBroker.Object,
                _quickInfoBroker.Object);
            _broker = _brokerRaw;
        }

        [Fact]
        public void DismissDisplayWindows1()
        {
            _quickInfoBroker.Setup(x => x.IsQuickInfoActive(_textView.Object)).Returns(true).Verifiable();
            _quickInfoBroker.Setup(x => x.GetSessions(_textView.Object)).Returns(new List<IQuickInfoSession>().AsReadOnly()).Verifiable();
            _broker.DismissDisplayWindows();
            _quickInfoBroker.Verify();
        }
    }
}
