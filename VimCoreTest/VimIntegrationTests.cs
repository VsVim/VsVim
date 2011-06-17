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
            Assert.IsFalse(_vim.RemoveBuffer(view.Object));
        }

        [Test]
        public void RemoveBuffer2()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view);
            Assert.IsTrue(_vim.RemoveBuffer(view));
            Assert.IsTrue(_vim.GetBuffer(view).IsNone());
        }

        [Test]
        public void CreateBuffer1()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view);
            Assert.IsTrue(_vim.GetBuffer(view).IsSome());
            Assert.AreSame(view, _vim.GetBuffer(view).Value.TextView);
        }

        [Test,ExpectedException(typeof(ArgumentException))]
        public void CreateBuffer2()
        {
            var view = EditorUtil.CreateTextView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view);
            var vimBuffer2 = _vim.CreateBuffer(view);
        }

    }
}
