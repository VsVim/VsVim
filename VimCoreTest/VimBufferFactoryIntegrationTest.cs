using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimBufferFactoryIntegrationTest
    {
        private IVim _vim;
        private IVimBufferFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = EditorUtil.FactoryService.VimBufferFactory;
            _vim = EditorUtil.FactoryService.Vim;
        }

        [TearDown]
        public void TearDown()
        {

        }

        /// <summary>
        /// Ensure that CreateVimBuffer actually creates an IVimBuffer instance
        /// </summary>
        [Test]
        public void CreateVimBuffer_Simple()
        {
            var textView = EditorUtil.CreateTextView("");
            var vimTextBuffer = _factory.CreateVimTextBuffer(textView.TextBuffer, _vim);
            var buffer = _factory.CreateVimBuffer(textView, vimTextBuffer);
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
            var textView = EditorUtil.CreateTextView("");
            var vimTextBuffer = _factory.CreateVimTextBuffer(textView.TextBuffer, _vim);
            var buffer1 = _factory.CreateVimBuffer(textView, vimTextBuffer);
            var buffer2 = _factory.CreateVimBuffer(textView, vimTextBuffer);
            Assert.AreNotSame(buffer1, buffer2);
        }
    }
}
