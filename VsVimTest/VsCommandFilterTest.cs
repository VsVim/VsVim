using System;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;

namespace VsVim.UnitTest
{
    [TestFixture]
    public class VsCommandFilterTest
    {
        private Mock<IVimBuffer> _buffer;
        private Mock<IVsTextView> _textView;
        private Mock<IServiceProvider> _serviceProvider;
        private Mock<IVsExtensibility> _vsExt;
        private VsCommandFilter _filter;

        [SetUp]
        public void SetUp()
        {
            _buffer = new Mock<IVimBuffer>();
            _textView = new Mock<IVsTextView>();
            _vsExt = new Mock<IVsExtensibility>(MockBehavior.Strict);
            _vsExt.Setup(x => x.IsInAutomationFunction()).Returns(0);
            _serviceProvider = MockObjectFactory.CreateServiceProvider(Tuple.Create(typeof(IVsExtensibility), (object)_vsExt.Object));
            _filter = new VsCommandFilter(_buffer.Object, _textView.Object, _serviceProvider.Object);
        }

        private void AssertCannotConvert2K(VSConstants.VSStd2KCmdID id)
        {
            KeyInput ki;
            Assert.IsFalse(_filter.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
        }

        private void AssertCanConvert2K(VSConstants.VSStd2KCmdID id, KeyInput expected)
        {
            KeyInput ki;
            Assert.IsTrue(_filter.TryConvert(VSConstants.VSStd2K, (uint)id, IntPtr.Zero, out ki));
            Assert.AreEqual(expected, ki);
        }

        [Test]
        public void TryConvert1()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.TabKey);
        }

        [Test]
        public void TryConvert2()
        {
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(false);
            AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
        }

        [Test, Description("Don't convert keys when in automation")]
        public void TryConvert3()
        {
            _vsExt.Setup(x => x.IsInAutomationFunction()).Returns(1);
            _buffer.Setup(x => x.CanProcess(It.IsAny<KeyInput>())).Returns(true);
            AssertCannotConvert2K(VSConstants.VSStd2KCmdID.TAB);
        }


    }
}
