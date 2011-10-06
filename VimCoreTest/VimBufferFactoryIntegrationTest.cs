using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using System;
using NUnit.Mocks;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimBufferFactoryIntegrationTest : VimTestBase
    {
        private IVim _vim;
        private IVimBufferFactory _vimBufferFactory;

        [SetUp]
        public void SetUp()
        {
            _vimBufferFactory = VimBufferFactory;
            _vim = Vim;
        }

        /// <summary>
        /// Ensure that CreateVimBuffer actually creates an IVimBuffer instance
        /// </summary>
        [Test]
        public void CreateVimBuffer_Simple()
        {
            var textView = CreateTextView("");
            var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textView.TextBuffer, _vim);
            var buffer = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
            Assert.IsNotNull(buffer);
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
            Assert.AreSame(vimTextBuffer, buffer.VimTextBuffer);
        }

        /// <summary>
        /// The IVimBufferFactory should be stateless and happily create multiple IVimBuffer instances for a 
        /// given ITextView (even though at an application level that will be illegal)
        /// </summary>
        [Test]
        public void CreateVimBuffer_Stateless()
        {
            var textView = CreateTextView("");
            var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textView.TextBuffer, _vim);
            var buffer1 = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
            var buffer2 = _vimBufferFactory.CreateVimBuffer(textView, vimTextBuffer);
            Assert.AreNotSame(buffer1, buffer2);
        }

        /// <summary>
        /// Create the IVimBuffer for an uninitialized ITextView instance.  This should create an 
        /// IVimBuffer in the uninitialized state 
        /// </summary>
        [Test]
        public void CreateVimBuffer_UninitializedTextView()
        {
            var textBuffer = CreateTextBuffer("");
            var textView = MockObjectFactory.CreateTextView(textBuffer);
            textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
            var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);
            var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
            Assert.AreEqual(ModeKind.Uninitialized, vimBuffer.ModeKind);
        }

        /// <summary>
        /// Once an ITextView state is initialized the IVimBuffer should move to the appropriate state
        /// </summary>
        [Test]
        public void CreateVimBuffer_TextViewDelayInitialize()
        {
            var textBuffer = CreateTextBuffer("");
            var textView = MockObjectFactory.CreateTextView(textBuffer);
            textView.SetupGet(x => x.TextViewLines).Returns((ITextViewLineCollection)null);
            var vimTextBuffer = _vimBufferFactory.CreateVimTextBuffer(textBuffer, _vim);
            var vimBuffer = _vimBufferFactory.CreateVimBuffer(textView.Object, vimTextBuffer);
            Assert.AreEqual(ModeKind.Uninitialized, vimBuffer.ModeKind);

            textView.SetupGet(x => x.TextViewLines).Returns(new Mock<ITextViewLineCollection>().Object);
            textView.Raise(x => x.LayoutChanged += null, (TextViewLayoutChangedEventArgs)null);

            Assert.AreEqual(ModeKind.Normal, vimBuffer.ModeKind);
        }
    }
}
