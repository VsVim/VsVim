using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class VimIntegrationTests
    {
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _vim = EditorUtil.FactoryService.Vim;
        }

        [Test]
        public void RemoveBuffer1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            Assert.IsFalse(_vim.RemoveVimBuffer(view.Object));
        }

        [Test]
        public void RemoveBuffer2()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.IsTrue(_vim.RemoveVimBuffer(view));
            Assert.IsTrue(_vim.GetVimBuffer(view).IsNone());
        }

        [Test]
        public void CreateVimBuffer1()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.IsTrue(_vim.GetVimBuffer(view).IsSome());
            Assert.AreSame(view, _vim.GetVimBuffer(view).Value.TextView);
        }

        [Test,ExpectedException(typeof(ArgumentException))]
        public void CreateVimBuffer2()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            var vimBuffer2 = _vim.CreateVimBuffer(view);
        }

    }
}
