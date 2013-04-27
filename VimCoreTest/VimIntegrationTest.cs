using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public abstract class VimIntegrationTest : VimTestBase
    {
        public sealed class MistTest : VimIntegrationTest
        {
            [Fact]
            public void RemoveBuffer1()
            {
                var view = new Mock<IWpfTextView>(MockBehavior.Strict);
                Assert.False(Vim.RemoveVimBuffer(view.Object));
            }

            [Fact]
            public void RemoveBuffer2()
            {
                var view = CreateTextView("foo bar");
                var vimBuffer = Vim.CreateVimBuffer(view);
                Assert.True(Vim.RemoveVimBuffer(view));

                Assert.False(Vim.TryGetVimBuffer(view, out vimBuffer));
            }

            [Fact]
            public void CreateVimBuffer1()
            {
                var view = CreateTextView("foo bar");
                var vimBuffer = Vim.CreateVimBuffer(view);

                IVimBuffer found;
                Assert.True(Vim.TryGetVimBuffer(view, out found));
                Assert.Same(view, found.TextView);
            }

            [Fact]
            public void CreateVimBuffer2()
            {
                var view = CreateTextView("foo bar");
                var vimBuffer = Vim.CreateVimBuffer(view);
                Assert.Throws<ArgumentException>(() => Vim.CreateVimBuffer(view));
            }
        }

        public sealed class DisableAllTest : VimIntegrationTest
        {
            /// <summary>
            /// Check disable with a single IVimBuffer
            /// </summary>
            [Fact]
            public void One()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.Disabled, vimBuffer.ModeKind);
            }

            /// <summary>
            /// Check disable with multiple IVimBuffer instances
            /// </summary>
            [Fact]
            public void Multiple()
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
            public void MultipleReenable()
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
}
