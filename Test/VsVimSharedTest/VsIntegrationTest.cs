using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.VisualStudio.Implementation.Misc;
using Vim.VisualStudio.UnitTest.Utils;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// Used to simulate integration scenarios with Visual Studio
    /// </summary>
    public abstract class VsIntegrationTest : VimTestBase
    {
        private VsSimulation _vsSimulation;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IVimBuffer _vimBuffer;
        private IVimBufferCoordinator _bufferCoordinator;

        /// <summary>
        /// Create a Visual Studio simulation with the specified set of lines
        /// </summary>
        protected virtual void Create(params string[] lines)
        {
            CreateCore(simulateResharper: false, usePeekRole: false, lines: lines);
        }

        protected virtual void CreatePeek(params string[] lines)
        {
            CreateCore(simulateResharper: false, usePeekRole: true, lines: lines);
        }

        /// <summary>
        /// Create a Visual Studio simulation with the specified set of lines
        /// </summary>
        private void CreateCore(bool simulateResharper, bool usePeekRole, params string[] lines)
        {
            if (usePeekRole)
            {
                _textBuffer = CreateTextBuffer(lines);
                _textView = TextEditorFactoryService.CreateTextView(
                    _textBuffer,
                    TextEditorFactoryService.CreateTextViewRoleSet(PredefinedTextViewRoles.Document, PredefinedTextViewRoles.Editable, Constants.TextViewRoleEmbeddedPeekTextView));
            }
            else
            {
                _textView = CreateTextView(lines);
                _textBuffer = _textView.TextBuffer;
            }
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_vimBuffer);
            _vsSimulation = new VsSimulation(
                _bufferCoordinator,
                simulateResharper: simulateResharper,
                simulateStandardKeyMappings: false,
                editorOperationsFactoryService: EditorOperationsFactoryService,
                keyUtil: KeyUtil);

            VimHost.TryCustomProcessFunc = (textView, insertCommand) =>
                {
                    if (textView == _textView)
                    {
                        return _vsSimulation.VsCommandTarget.TryCustomProcess(insertCommand);
                    }

                    return false;
                };
        }

        public sealed class BackspaceAndTabTest : VsIntegrationTest
        {
            /// <summary>
            /// As long as the Visual Studio controls tabs and backspace then the 'backspace' setting will
            /// not be respected 
            /// </summary>
            [Fact]
            public void IgnoreBackspaceSetting()
            {
                Create("cat");
                _vsSimulation.VimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(true);
                _vimBuffer.GlobalSettings.Backspace = "";
                _vimBuffer.ProcessNotation("A<BS>");
                Assert.Equal("ca", _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void RespectBackspaceSetting()
            {
                Create("cat");
                _vsSimulation.VimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(false);
                _vimBuffer.GlobalSettings.Backspace = "";
                _vimBuffer.ProcessNotation("a<BS>");
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal(1, VimHost.BeepCount);
            }

            [Fact]
            public void IgnoreTab()
            {
                Create("");
                _vsSimulation.VimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(true);
                _vimBuffer.LocalSettings.ExpandTab = true;
                _vimBuffer.LocalSettings.SoftTabStop = 3;
                _vimBuffer.ProcessNotation("i<Tab>");
                Assert.Equal(new string(' ', 8), _textBuffer.GetLine(0).GetText());
            }

            [Fact]
            public void RespectTab()
            {
                Create("");
                _vsSimulation.VimApplicationSettings.SetupGet(x => x.UseEditorTabAndBackspace).Returns(false);
                _vimBuffer.LocalSettings.ExpandTab = true;
                _vimBuffer.LocalSettings.SoftTabStop = 3;
                _vimBuffer.ProcessNotation("i<Tab>");
                Assert.Equal(new string(' ', 3), _textBuffer.GetLine(0).GetText());
            }
        }

        public sealed class KeyMapTest : VsIntegrationTest
        {
            /// <summary>
            /// Make sure that S_RETURN will actually come across as such 
            /// </summary>
            [Fact]
            public void ShiftAndReturn()
            {
                Create("cat", "dog");
                _vimBuffer.Process(":map <S-RETURN> o<Esc>", enter: true);
                _vsSimulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Shift));
                Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
            }

            /// <summary>
            /// Make sure that S_TAB will actually come across as such 
            /// </summary>
            [Fact]
            public void ShiftAndTab()
            {
                Create("cat", "dog");
                _vimBuffer.Process(":map <S-TAB> o<Esc>", enter: true);
                _vsSimulation.Run(KeyInputUtil.ApplyModifiersToChar('\t', KeyModifiers.Shift));
                Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
                Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
                Assert.Equal("", _textBuffer.GetLine(1).GetText());
                Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
            }

            [Fact]
            public void ShiftAndEnter()
            {
                Create("cat", "dog");
                _vimBuffer.Process(":inoremap <S-CR> <Esc>", enter: true);
                _vimBuffer.Process("i");
                _vsSimulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Shift));
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Regression test for issue #663.  Previously the ; was being seen as part of a possible dead
            /// key combination and mapping wasn't kicking in.  Now that we know it can't be part of a dead
            /// key mapping we process it promptly as it should be 
            /// </summary>
            [Fact]
            public void DoubleSemicolon()
            {
                Create("cat", "dog");
                _vimBuffer.Process(":imap ;; <Esc>", enter: true);
                _vimBuffer.Process("i");
                _vsSimulation.Run(";;");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that keys which are mapped to display window keys are passed down to 
            /// Visual Studio as mapped keys 
            /// </summary>
            [Fact]
            public void MappedDisplayWindowKey()
            {
                Create("cat", "dog");
                _vimBuffer.Process(":imap <Tab> <Down>", enter: true);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vsSimulation.DisplayWindowBroker.SetupGet(x => x.IsCompletionActive).Returns(true);
                _vsSimulation.Run(VimKey.Tab);
                Assert.Equal(_textBuffer.GetLine(1).Start, _textView.GetCaretPoint());
            }
        }

        public sealed class MiscTest : VsIntegrationTest
        {
            /// <summary>
            /// Simple sanity check to ensure that our simulation is working properly
            /// </summary>
            [Fact]
            public void Insert_SanityCheck()
            {
                Create("hello world");
                _textView.MoveCaretTo(0);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vsSimulation.Run('x');
                Assert.Equal("xhello world", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// Make sure that Escape dismisses intellisense even in normal mode
            /// </summary>
            [Fact]
            public void NormalMode_EscapeShouldDismissCompletion()
            {
                Create("cat dog");
                _vsSimulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
                _vsSimulation.DisplayWindowBroker.Setup(x => x.DismissDisplayWindows()).Verifiable();
                _vsSimulation.Run(VimKey.Escape);
                _vsSimulation.DisplayWindowBroker.Verify();
            }

            /// <summary>
            /// Keys like j, k should go to normal mode even when Intellisense is active
            /// </summary>
            [Fact]
            public void NormalMode_CommandKeysGoToVim()
            {
                Create("cat dog");
                _vsSimulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
                _vsSimulation.Run("dw");
                Assert.Equal("dog", _textBuffer.GetLine(0).GetText());
            }

            /// <summary>
            /// Arrow keys and the like should go through Visual Studio when intellisense is 
            /// active
            /// </summary>
            [Fact]
            public void NormalMode_ArrowKeysGoToVisualStudio()
            {
                Create("cat", "dog");
                var didProcess = false;
                _vimBuffer.KeyInputProcessed += delegate { didProcess = false; };
                _vsSimulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
                _vsSimulation.Run(VimKey.Down);
                Assert.False(didProcess);
            }

            /// <summary>
            /// Without any mappings the Shift+Down should extend the selection downwards and cause us to
            /// enter Visual Mode
            /// </summary>
            [Fact]
            public void StandardCommand_ExtendSelectionDown()
            {
                Create("dog", "cat", "tree");
                _vsSimulation.SimulateStandardKeyMappings = true;
                _vsSimulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Down, KeyModifiers.Shift));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Without any mappings the Shift+Right should extend the selection downwards and cause us to
            /// enter Visual Mode
            /// </summary>
            [Fact]
            public void StandardCommand_ExtendSelectionRight()
            {
                Create("dog", "cat", "tree");
                _vsSimulation.SimulateStandardKeyMappings = true;
                _vsSimulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift));
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure the Insert key correctly toggles to insert mode then replace
            /// </summary>
            [Fact]
            public void SwitchMode_InsertKey()
            {
                Create("");
                _vsSimulation.Run(VimKey.Insert);
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                _vsSimulation.Run(VimKey.Insert);
                Assert.Equal(ModeKind.Replace, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that we allow keys like down to make it directly to Insert mode when there is
            /// an active IWordCompletionSession
            /// </summary>
            [Fact]
            public void WordCompletion_Down()
            {
                Create("c dog", "cat copter");
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.MoveCaretTo(1);
                _vsSimulation.Run(KeyNotationUtil.StringToKeyInput("<C-n>"));
                _vsSimulation.Run(KeyNotationUtil.StringToKeyInput("<Down>"));
                Assert.Equal("copter dog", _textView.GetLine(0).GetText());
            }

            /// <summary>
            /// When there is an active IWordCompletionSession we want to let even direct input go directly
            /// to insert mode.  
            /// </summary>
            [Fact]
            public void WordCompletion_TypeChar()
            {
                Create("c dog", "cat");
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _textView.MoveCaretTo(1);
                _vsSimulation.Run(KeyNotationUtil.StringToKeyInput("<C-n>"));
                _vsSimulation.Run('s');
                Assert.Equal("cats dog", _textView.GetLine(0).GetText());
                Assert.True(_vimBuffer.InsertMode.ActiveWordCompletionSession.IsNone());
            }
        }

        public sealed class EscapeTest : VsIntegrationTest
        {
            [Fact]
            public void DismissPeekDefinitionWindow()
            {
                CreatePeek("cat dog");
                _vsSimulation.Run(VimKey.Escape);
                Assert.Equal(KeyInputUtil.EscapeKey, _vsSimulation.VsSimulationCommandTarget.LastExecEditCommand.KeyInput);
            }

            /// <summary>
            /// The Escape key shouldn't dismiss the peek definition window when we are in 
            /// insert mode
            /// </summary>
            [Fact]
            public void DontDismissPeekDefinitionWindow()
            {
                CreatePeek("cat dog");
                _vsSimulation.Run("i");
                Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
                _vsSimulation.Run(VimKey.Escape);
                Assert.Null(_vsSimulation.VsSimulationCommandTarget.LastExecEditCommand);
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }

            /// <summary>
            /// In a normal window the Escape key should cause a beep to occur when the buffer is
            /// in normal mode 
            /// </summary>
            [Fact]
            public void BeepNormalMode()
            {
                Create();
                int count = 0;
                _vimBuffer.KeyInputProcessed += delegate { count++; };
                _vsSimulation.Run(VimKey.Escape);
                Assert.Equal(1, count);
                Assert.Null(_vsSimulation.VsSimulationCommandTarget.LastExecEditCommand);
            }
        }

        public abstract class ReSharperTest : VsIntegrationTest
        {
            public sealed class BackTest : ReSharperTest
            {
                /// <summary>
                /// Verify that the back behavior which R# works as expected when we are in 
                /// Insert mode.  It should delete the simple double matched parens
                /// </summary>
                [Fact]
                public void ParenWorksInInsert()
                {
                    Create("method();", "next");
                    _textView.MoveCaretTo(7);
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                    _vsSimulation.Run(VimKey.Back);
                    Assert.Equal("method;", _textView.GetLine(0).GetText());
                }

                /// <summary>
                /// Make sure that back can be used to navigate across an entire line.  Briefly introduced
                /// an issue during the testing of the special casing of Back which caused the key to be
                /// disabled for a time
                /// </summary>
                [Fact]
                public void AcrossEntireLine()
                {
                    Create("hello();", "world");
                    _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                    _textView.MoveCaretTo(8);
                    for (int i = 0; i < 8; i++)
                    {
                        _vsSimulation.Run(VimKey.Back);
                        Assert.Equal(8 - (i + 1), _textView.GetCaretPoint().Position);
                    }
                }

                /// <summary>
                /// Ensure the repeating of the Back command is done properly for Resharper.  We special case
                /// the initial handling of the command.  But this shouldn't affect the repeat as it should
                /// be using CustomProcess under the hood
                /// </summary>
                [Fact]
                public void Repeat()
                {
                    Create("dog toy", "fish chips");
                    _vimBuffer.GlobalSettings.Backspace = "start";
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                    _textView.MoveCaretToLine(1, 5);
                    _vsSimulation.Run(VimKey.Back, VimKey.Escape);
                    _textView.MoveCaretTo(4);
                    _vsSimulation.Run(".");
                    Assert.Equal("dogtoy", _textView.GetLine(0).GetText());
                    Assert.Equal(2, _textView.GetCaretPoint().Position);
                }
            }

            public sealed class ReSharperEscapeTest : ReSharperTest
            {
                private int _escapeKeyCount;

                protected override void Create(params string[] lines)
                {
                    base.Create(lines);

                    _vimBuffer.KeyInputProcessed += (sender, e) =>
                    {
                        if (e.KeyInput.Key == VimKey.Escape)
                        {
                            _escapeKeyCount++;
                        }
                    };
                }

                /// <summary>
                /// The Escape key here needs to go to both R# and VsVim.  We have no way of manually dismissing the 
                /// intellisense displayed by R# and hence have to let them do it by letting them see the Escape 
                /// key themselves 
                /// </summary>
                [Fact]
                public void InsertWithIntellisenseActive()
                {
                    Create("blah");
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                    _reSharperCommandTarget.IntellisenseDisplayed = true;
                    _vsSimulation.Run(VimKey.Escape);
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    Assert.Equal(1, _reSharperCommandTarget.ExecEscapeCount);
                    Assert.Equal(1, _escapeKeyCount);
                    Assert.False(_reSharperCommandTarget.IntellisenseDisplayed);
                }

                /// <summary>
                /// We have no way to track whether or not R# intellisense is active.  Hence we have to act as if
                /// it is at all times even when it's not. 
                /// </summary>
                [Fact]
                public void InsertWithIntellisenseInactive()
                {
                    Create("blah");
                    _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                    _reSharperCommandTarget.IntellisenseDisplayed = false;
                    _vsSimulation.Run(VimKey.Escape);
                    Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
                    Assert.Equal(0, _reSharperCommandTarget.ExecEscapeCount);
                    Assert.Equal(1, _escapeKeyCount);
                    Assert.False(_reSharperCommandTarget.IntellisenseDisplayed);
                }
            }

            private ReSharperCommandTargetSimulation _reSharperCommandTarget;

            protected override void Create(params string[] lines)
            {
                CreateCore(simulateResharper: true, usePeekRole: false, lines: lines);
                _reSharperCommandTarget = _vsSimulation.ReSharperCommandTargetOpt;
            }
        }
    }
}
