using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class VimIntegrationTests : VimTestBase
    {
        private IVim _vim;

        [SetUp]
        public void Setup()
        {
            _vim = Vim;
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
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.IsTrue(_vim.RemoveVimBuffer(view));
            Assert.IsTrue(_vim.GetVimBuffer(view).IsNone());
        }

        [Test]
        public void CreateVimBuffer1()
        {
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.IsTrue(_vim.GetVimBuffer(view).IsSome());
            Assert.AreSame(view, _vim.GetVimBuffer(view).Value.TextView);
        }

        [Test,ExpectedException(typeof(ArgumentException))]
        public void CreateVimBuffer2()
        {
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            var vimBuffer2 = _vim.CreateVimBuffer(view);
        }

        /// <summary>
        /// Check disable with a single IVimBuffer
        /// </summary>
        [Test]
        public void DisableAll_One()
        {
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process(GlobalSettings.DisableAllCommand);
            Assert.AreEqual(ModeKind.Disabled, vimBuffer.ModeKind);
        }

        /// <summary>
        /// Check disable with multiple IVimBuffer instances
        /// </summary>
        [Test]
        public void DisableAll_Multiple()
        {
            var vimBuffer1 = CreateVimBuffer("hello world");
            var vimBuffer2 = CreateVimBuffer("hello world");
            vimBuffer1.Process(GlobalSettings.DisableAllCommand);
            Assert.AreEqual(ModeKind.Disabled, vimBuffer1.ModeKind);
            Assert.AreEqual(ModeKind.Disabled, vimBuffer2.ModeKind);
        }

        /// <summary>
        /// Check re-enable with multiple IVimBuffer instances
        /// </summary>
        [Test]
        public void DisableAll_MultipleReenable()
        {
            var vimBuffer1 = CreateVimBuffer("hello world");
            var vimBuffer2 = CreateVimBuffer("hello world");
            vimBuffer1.Process(GlobalSettings.DisableAllCommand);
            vimBuffer2.Process(GlobalSettings.DisableAllCommand);
            Assert.AreEqual(ModeKind.Normal, vimBuffer1.ModeKind);
            Assert.AreEqual(ModeKind.Normal, vimBuffer2.ModeKind);
        }
    }
}
