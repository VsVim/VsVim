using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class VimIntegrationTests
    {
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _vim = Utils.EditorUtil.FactoryService.vim;
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
            var view = EditorUtil.CreateView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view, "Some Name");
            Assert.IsTrue(_vim.RemoveBuffer(view));
            Assert.IsTrue(_vim.GetBuffer(view).IsNone());
        }

        [Test]
        public void CreateBuffer1()
        {
            var view = EditorUtil.CreateView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view, "Some Name");
            Assert.IsTrue(_vim.GetBuffer(view).IsSome());
            Assert.AreSame(view, _vim.GetBuffer(view).Value.TextView);
        }

        [Test,ExpectedException(typeof(ArgumentException))]
        public void CreateBuffer2()
        {
            var view = EditorUtil.CreateView("foo bar");
            var vimBuffer = _vim.CreateBuffer(view, "Some Name");
            var vimBuffer2 = _vim.CreateBuffer(view, "Some Name");
        }
    }
}
