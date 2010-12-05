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
        private Mock<IServiceProvider> _serviceProvider;
        private Mock<IVsExtensibility> _vsExt;
        private Mock<IExternalEditorManager> _externalEditorManager;
        private VsCommandFilter _filter;

        [SetUp]
        public void SetUp()
        {
            _buffer = new Mock<IVimBuffer>();
            _vsExt = new Mock<IVsExtensibility>(MockBehavior.Strict);
            _externalEditorManager = new Mock<IExternalEditorManager>(MockBehavior.Strict);
            _vsExt.Setup(x => x.IsInAutomationFunction()).Returns(0);
            _serviceProvider = MockObjectFactory.CreateServiceProvider(Tuple.Create(typeof(IVsExtensibility), (object)_vsExt.Object));

            var result = VsCommandFilter.Create(
                _buffer.Object, 
                (new Mock<IVsTextView>()).Object, 
                _serviceProvider.Object, 
                _externalEditorManager.Object);
            Assert.IsTrue(result.IsValue);
            _filter = result.Value;
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
            AssertCanConvert2K(VSConstants.VSStd2KCmdID.TAB, KeyInputUtil.VimKeyToKeyInput(VimKey.Tab));
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
