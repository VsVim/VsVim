using System;
using System.IO;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Path = System.IO.Path;
using Microsoft.FSharp.Core;

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

        public sealed class VimRcTest : VimTestBase
        {
            private readonly Mock<IFileSystem> _fileSystem;
            private readonly IFileSystem _originalFileSystem;
            private readonly Vim _vim;
            private readonly IVimGlobalSettings _globalSettings;

            public VimRcTest()
            {
                _vim = (Vim)Vim;
                _globalSettings = Vim.GlobalSettings;
                _fileSystem = new Mock<IFileSystem>();
                _originalFileSystem = _vim._fileSystem;
                _vim._fileSystem = _fileSystem.Object;
                VimHost.CreateHiddenTextViewFunc = () => TextEditorFactoryService.CreateTextView();
            }

            public override void Dispose()
            {
                base.Dispose();
                _vim._fileSystem = _originalFileSystem;
            }

            private void Run(string vimRcText)
            {
                var lines = vimRcText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                _fileSystem.Setup(x => x.LoadVimRcContents()).Returns(new FSharpOption<FileContents>(new FileContents("_vimrc", lines)));
                Assert.True(Vim.LoadVimRc().IsLoadSucceeded);
            }

            [Fact]
            public void Simple()
            {
                Assert.False(_globalSettings.HighlightSearch);
                Run("set hlsearch");
                Assert.True(_globalSettings.HighlightSearch);
            }

            /// <summary>
            /// Don't run the contents of a function body.  They should only be parsed here 
            /// </summary>
            [Fact]
            public void FunctionContents()
            {
                var text = @"
function Test() 
  set hlsearch
endfunction
let x = 42
";
                Run(text);
                Assert.False(_globalSettings.HighlightSearch);
                Assert.Equal(42, Vim.VariableMap["x"].AsNumber().Item);
            }

            /// <summary>
            /// Make sure this code can handle the case where the vimrc file has colons at the start of the
            /// lines.  Introduced a bug during the development of 1.4.0 that regressed this because of a 
            /// combination of other features
            /// </summary>
            [Fact]
            public void HasColons()
            {
                var text = @"
:set incsearch
:set ts=4
:set sw=4
";
                Run(text);
                Assert.True(_globalSettings.IncrementalSearch);
                Assert.Equal(4, _vim._vimRcLocalSettings.TabStop);
                Assert.Equal(4, _vim._vimRcLocalSettings.ShiftWidth);
            }
        }
    }
}
