using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class VimIntegrationTests : VimTestBase
    {
        private readonly IVim _vim;

        public VimIntegrationTests()
        {
            _vim = Vim;
        }

        [Fact]
        public void RemoveBuffer1()
        {
            var view = new Mock<IWpfTextView>(MockBehavior.Strict);
            Assert.False(_vim.RemoveVimBuffer(view.Object));
        }

        [Fact]
        public void RemoveBuffer2()
        {
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.True(_vim.RemoveVimBuffer(view));
            Assert.True(_vim.GetVimBuffer(view).IsNone());
        }

        [Fact]
        public void CreateVimBuffer1()
        {
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.True(_vim.GetVimBuffer(view).IsSome());
            Assert.Same(view, _vim.GetVimBuffer(view).Value.TextView);
        }

        [Fact]
        public void CreateVimBuffer2()
        {
            var view = CreateTextView("foo bar");
            var vimBuffer = _vim.CreateVimBuffer(view);
            Assert.Throws<ArgumentException>(() => _vim.CreateVimBuffer(view));
        }

        /// <summary>
        /// Check disable with a single IVimBuffer
        /// </summary>
        [Fact]
        public void DisableAll_One()
        {
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process(GlobalSettings.DisableAllCommand);
            Assert.Equal(ModeKind.Disabled, vimBuffer.ModeKind);
        }

        /// <summary>
        /// Check disable with multiple IVimBuffer instances
        /// </summary>
        [Fact]
        public void DisableAll_Multiple()
        {
            var vimBuffer1 = CreateVimBuffer("hello world");
            var vimBuffer2 = CreateVimBuffer("hello world");
            vimBuffer1.Process(GlobalSettings.DisableAllCommand);
            Assert.Equal(ModeKind.Disabled, vimBuffer1.ModeKind);
            Assert.Equal(ModeKind.Disabled, vimBuffer2.ModeKind);
        }

        /// <summary>
        /// Check re-enable with multiple IVimBuffer instances
        /// </summary>
        [Fact]
        public void DisableAll_MultipleReenable()
        {
            var vimBuffer1 = CreateVimBuffer("hello world");
            var vimBuffer2 = CreateVimBuffer("hello world");
            vimBuffer1.Process(GlobalSettings.DisableAllCommand);
            vimBuffer2.Process(GlobalSettings.DisableAllCommand);
            Assert.Equal(ModeKind.Normal, vimBuffer1.ModeKind);
            Assert.Equal(ModeKind.Normal, vimBuffer2.ModeKind);
        }
    }
}
