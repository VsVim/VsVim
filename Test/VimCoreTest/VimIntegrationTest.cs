﻿using System;
using System.IO;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Xunit.Extensions;
using Microsoft.FSharp.Core;
using Vim.UnitTest.Mock;
using EditorUtils;

namespace Vim.UnitTest
{
    public abstract class VimIntegrationTest : VimTestBase
    {
        public sealed class MiscTest : VimIntegrationTest
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

            /// <summary>
            /// If we are in the middle of an incremental search and the mode changes the search should be
            /// cancelled 
            /// </summary>
            [Fact]
            public void ModeSwitchResetIncrementalSearch()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation("/wo");
                Assert.True(vimBuffer.IncrementalSearch.InSearch);
                vimBuffer.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                Assert.False(vimBuffer.IncrementalSearch.InSearch);
            }

            /// <summary>
            /// Make sure this doesn't leave a hanging empty linked undo transaction
            /// </summary>
            [Fact]
            public void CloseSimpleAppend()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation("a");
                vimBuffer.Close();
            }

            /// <summary>
            /// Make sure this doesn't leave a hanging empty linked undo transaction
            /// </summary>
            [Fact]
            public void LeaveSimpleAppend()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation("a<Esc>");
                vimBuffer.Close();
            }

            /// <summary>
            /// Make sure this doesn't leave a hanging empty linked undo transaction
            /// </summary>
            [Fact]
            public void LeaveCountedInsert()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation("3i<Esc>");
                vimBuffer.Close();
            }

            /// <summary>
            /// Make sure external selection events that include end of line function even if VE
            /// prevents it. 
            /// </summary>
            [Fact]
            public void EndOfLineSelection()
            {
                var vimBuffer = CreateVimBuffer("cat", "dog", "tree");
                vimBuffer.GlobalSettings.VirtualEdit = "";
                var textBuffer = vimBuffer.TextBuffer;
                var textSnapshot = textBuffer.CurrentSnapshot;
                var selection = vimBuffer.TextView.Selection;
                var span = textBuffer.GetLineRange(0);
                selection.Select(span.ExtentIncludingLineBreak);
                Assert.Equal(span.ExtentIncludingLineBreak, selection.StreamSelectionSpan.SnapshotSpan);
                Assert.Equal(textSnapshot.Version.VersionNumber, textBuffer.CurrentSnapshot.Version.VersionNumber);
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
                _fileSystem.Setup(x => x.GetVimRcDirectories()).Returns(new string[] { });
                _originalFileSystem = _vim.FileSystem;
                _vim.FileSystem = _fileSystem.Object;
                VimHost.CreateHiddenTextViewFunc = () => TextEditorFactoryService.CreateTextView();
            }

            public override void Dispose()
            {
                base.Dispose();
                _vim.FileSystem = _originalFileSystem;
            }

            private void Run(string vimRcText)
            {
                var vimRcPath = new VimRcPath(VimRcKind.VimRc, "_vimrc");
                var lines = vimRcText.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                _fileSystem
                    .Setup(x => x.GetVimRcFilePaths())
                    .Returns(new[] { vimRcPath });
                _fileSystem
                    .Setup(x => x.ReadAllLines(vimRcPath.FilePath))
                    .Returns(FSharpOption.Create(lines));
                Assert.True(Vim.LoadVimRc().IsLoadSucceeded);
            }

            private void RunNone()
            {
                _fileSystem
                    .Setup(x => x.GetVimRcFilePaths())
                    .Returns(new VimRcPath[] { });
                Assert.True(Vim.LoadVimRc().IsLoadFailed);
            }

            [Theory,
            InlineData(@"set shellcmdflag=-lic", @"-lic"),
            InlineData(@"set shellcmdflag=sh", @"sh")]
            public void ShellFlag(string command, string expected)
            {
                Run(command);
                Assert.Equal(expected, _globalSettings.ShellFlag);
            }

            [Theory,
             InlineData(@"set shell=sh.exe", @"sh.exe"),
             InlineData(@"set shell=c:\1\sh.exe", @"c:\1\sh.exe"),
             InlineData(@"set shell=c:\s\sh.exe", @"c:\s\sh.exe"),
             InlineData(@"set shell=c:\sss\sh.exe", @"c:\sss\sh.exe"),
             InlineData(@"set shell=c:\sh.exe", @"c:\sh.exe")]
            public void Shell(string command, string expected)
            {
                Run(command);
                Assert.Equal(expected, _globalSettings.Shell);
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

            /// <summary>
            /// Make sure that we handle autocmd correctly when it comes from the _vimrc file 
            /// </summary>
            [Fact]
            public void SimpleAutoCommand()
            {
                var text = @"
autocmd BufEnter *.html set ts=12
";
                Run(text);
                var vimBuffer = CreateVimBufferWithName("test.html");
                Assert.Equal(12, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void SpacesBeforeCommands()
            {
                var text = @"
    set hlsearch
";
                Run(text);
                Assert.True(_globalSettings.HighlightSearch);
            }

            [Fact]
            public void Errors()
            {
                var text = @"
    set foo=1
";
                Run(text);

                var errorArray = ((VimRcState.LoadSucceeded)_vim.VimRcState).Item2;
                Assert.Equal(1, errorArray.Length);
                Assert.Equal(Resources.Interpreter_UnknownOption("foo"), errorArray[0]);
            }

            [Fact]
            public void DefaultSettings73()
            {
                VimHost.DefaultSettings = DefaultSettings.GVim73;
                RunNone();
                Assert.True(_globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceStart);
                Assert.Equal("", _globalSettings.SelectMode);
            }

            [Fact]
            public void DefaultSettings7g()
            {
                VimHost.DefaultSettings = DefaultSettings.GVim74;
                RunNone();
                Assert.True(_globalSettings.IsBackspaceEol && _globalSettings.IsBackspaceIndent && _globalSettings.IsBackspaceStart);
                Assert.Equal("mouse,key", _globalSettings.SelectMode);
                Assert.Equal("popup", _globalSettings.MouseModel);
            }
        }

        public sealed class DisplayPatternTest : VimIntegrationTest
        {
            private bool _didRun;

            public DisplayPatternTest()
            {
                Vim.GlobalSettings.HighlightSearch = true;
                VimData.LastSearchData = new SearchData("cat", SearchPath.Forward);
                VimData.DisplayPatternChanged += delegate { _didRun = true; };
            }

            /// <summary>
            /// The repeat last search should cause the display of tags to be resumed
            /// </summary>
            [Fact]
            public void RepeatLastSearch()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation(@":nohl", enter: true);
                Assert.Equal("", VimData.DisplayPattern);
                vimBuffer.ProcessNotation(@"n");
                Assert.True(_didRun);
                Assert.Equal("cat", VimData.DisplayPattern);
            }

            /// <summary>
            /// The next word under cursor command '*' should cause the SearhRan event to fire
            /// </summary>
            [Fact]
            public void NextWordUnderCaret()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.Process("*");
                Assert.True(_didRun);
            }

            /// <summary>
            /// The '/' command should register a search change after the Enter key
            /// </summary>
            [Fact]
            public void IncrementalSerach()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.Process("/ab");
                Assert.False(_didRun);
                vimBuffer.Process(VimKey.Enter);
                Assert.True(_didRun);
            }

            /// <summary>
            /// The :s command should cause tags to be resumed
            /// </summary>
            [Fact]
            public void SubstituteCommand()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.ProcessNotation(@":nohl", enter: true);
                vimBuffer.ProcessNotation(":s/dog/bat<Enter>");
                Assert.True(_didRun);
                Assert.Equal("dog", VimData.DisplayPattern);
            }

            /// <summary>
            /// Don't raise the SearchRan command for simple commands
            /// </summary>
            [Fact]
            public void DontRaiseForSimpleCommands()
            {
                var vimBuffer = CreateVimBuffer("hello world");
                vimBuffer.Process("dd");
                Assert.False(_didRun);
            }
        }
    }
}
