using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.UI.Wpf.Implementation.Misc;

namespace Vim.UI.Wpf.UnitTest
{
    public class DisplayWindowBrokerTest
    {
        private readonly Mock<ISmartTagBroker> _smartTagBroker;
        private readonly Mock<ICompletionBroker> _completionBroker;
        private readonly Mock<ISignatureHelpBroker> _signatureBroker;
        private readonly Mock<IQuickInfoBroker> _quickInfoBroker;
        private readonly Mock<ITextView> _textView;
        private readonly DisplayWindowBroker _brokerRaw;
        private readonly IDisplayWindowBroker _broker;

        public DisplayWindowBrokerTest()
        {
            _smartTagBroker = new Mock<ISmartTagBroker>();
            _completionBroker = new Mock<ICompletionBroker>();
            _signatureBroker = new Mock<ISignatureHelpBroker>();
            _quickInfoBroker = new Mock<IQuickInfoBroker>();
            _textView = new Mock<ITextView>();
            _brokerRaw = new DisplayWindowBroker(
                _textView.Object,
                _completionBroker.Object,
                _signatureBroker.Object,
                _smartTagBroker.Object,
                _quickInfoBroker.Object);
            _broker = _brokerRaw;
        }

        [Fact]
        public void IsSmartTagSessionActive1()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(false).Verifiable();
            Assert.False(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Fact]
        public void IsSmartTagSessionActive2()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns((new List<ISmartTagSession>()).AsReadOnly())
                .Verifiable();
            Assert.False(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Fact]
        public void IsSmartTagSessionActive3()
        {
            var session = new Mock<ISmartTagSession>();
            session.SetupGet(x => x.State).Returns(SmartTagState.Collapsed);
            var list = Enumerable.Repeat(session.Object, 1).ToList().AsReadOnly();
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns(list)
                .Verifiable();
            Assert.False(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Fact]
        public void IsSmartTagSessionActive4()
        {
            var session = new Mock<ISmartTagSession>();
            session.SetupGet(x => x.State).Returns(SmartTagState.Expanded);
            var list = Enumerable.Repeat(session.Object, 1).ToList().AsReadOnly();
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns(list)
                .Verifiable();
            Assert.True(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
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
