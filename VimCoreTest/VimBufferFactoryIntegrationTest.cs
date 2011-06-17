using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VimBufferFactoryIntegrationTest
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

        [Test]
        public void CreateBuffer1()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var buffer = _factory.CreateBuffer(_vim, view);
            Assert.IsNotNull(buffer);
            Assert.AreEqual(ModeKind.Normal, buffer.ModeKind);
        }

        [Test,Description("Factory has no state and should be able to create multiple IVimBuffer for the same IWpfTextView")]
        public void CreateBuffer2()
        {
            var view1 = EditorUtil.CreateTextView("foo bar");
            var view2 = EditorUtil.CreateTextView("foo bar");
            var buffer1 = _factory.CreateBuffer(_vim, view1);
            Assert.IsNotNull(buffer1);
            var buffer2 = _factory.CreateBuffer(_vim, view2);
            Assert.IsNotNull(buffer2);
            Assert.AreNotSame(buffer1, buffer2);
        }
    }
}
