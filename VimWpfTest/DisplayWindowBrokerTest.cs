using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.UI.Wpf.Implementation;

namespace Vim.UI.Wpf.UnitTest
{
    [TestFixture]
    public class DisplayWindowBrokerTest
    {
        private Mock<ISmartTagBroker> _smartTagBroker;
        private Mock<ICompletionBroker> _completionBroker;
        private Mock<ISignatureHelpBroker> _signatureBroker;
        private Mock<IQuickInfoBroker> _quickInfoBroker;
        private Mock<ITextView> _textView;
        private DisplayWindowBroker _brokerRaw;
        private IDisplayWindowBroker _broker;

        [SetUp]
        public void SetUp()
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

        [Test]
        public void IsSmartTagSessionActive1()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(false).Verifiable();
            Assert.IsFalse(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void IsSmartTagSessionActive2()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns((new List<ISmartTagSession>()).AsReadOnly())
                .Verifiable();
            Assert.IsFalse(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Test]
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
            Assert.IsFalse(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Test]
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
            Assert.IsTrue(_broker.IsSmartTagSessionActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void DismissDisplayWindows1()
        {
            _quickInfoBroker.Setup(x => x.IsQuickInfoActive(_textView.Object)).Returns(true).Verifiable();
            _quickInfoBroker.Setup(x => x.GetSessions(_textView.Object)).Returns(new List<IQuickInfoSession>().AsReadOnly()).Verifiable();
            _broker.DismissDisplayWindows();
            _quickInfoBroker.Verify();
        }

    }
}
