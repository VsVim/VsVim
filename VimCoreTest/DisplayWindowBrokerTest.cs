using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VimCoreTest
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
        public void IsSmartTagWindowActive1()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(false).Verifiable();
            Assert.IsFalse(_broker.IsSmartTagWindowActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void IsSmartTagWindowActive2()
        {
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns((new List<ISmartTagSession>()).AsReadOnly())
                .Verifiable();
            Assert.IsFalse(_broker.IsSmartTagWindowActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void IsSmartTagWindowActive3()
        {
            var session = new Mock<ISmartTagSession>();
            session.SetupGet(x => x.State).Returns(SmartTagState.Collapsed);
            var list = Enumerable.Repeat(session.Object,1).ToList().AsReadOnly();
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns(list)
                .Verifiable();
            Assert.IsFalse(_broker.IsSmartTagWindowActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void IsSmartTagWindowActive4()
        {
            var session = new Mock<ISmartTagSession>();
            session.SetupGet(x => x.State).Returns(SmartTagState.Expanded);
            var list = Enumerable.Repeat(session.Object,1).ToList().AsReadOnly();
            _smartTagBroker.Setup(x => x.IsSmartTagActive(_textView.Object)).Returns(true).Verifiable();
            _smartTagBroker
                .Setup(x => x.GetSessions(_textView.Object))
                .Returns(list)
                .Verifiable();
            Assert.IsTrue(_broker.IsSmartTagWindowActive);
            _smartTagBroker.Verify();
        }

        [Test]
        public void DismissCompletionWindow1()
        {
            _quickInfoBroker.Setup(x => x.IsQuickInfoActive(_textView.Object)).Returns(true).Verifiable();
            _quickInfoBroker.Setup(x => x.GetSessions(_textView.Object)).Returns(new List<IQuickInfoSession>().AsReadOnly()).Verifiable();
            _broker.DismissCompletionWindow();
            _quickInfoBroker.Verify();
        }

    }
}
