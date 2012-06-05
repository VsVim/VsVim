using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using VsVim.Implementation;
using VsVim.UnitTest.Utils;

namespace VsVim.UnitTest
{
    /// <summary>
    /// Used to simulate integration scenarios with Visual Studio
    /// </summary>
    public sealed class VsIntegrationTest : VimTestBase
    {
        private VsSimulation _simulation;
        private ITextBuffer _textBuffer;
        private ITextView _textView;
        private IVimBuffer _vimBuffer;
        private IVimBufferCoordinator _bufferCoordinator;

        /// <summary>
        /// Create a Visual Studio simulation with the specified set of lines
        /// </summary>
        private void Create(params string[] lines)
        {
            Create(false, lines);
        }

        /// <summary>
        /// Create a Visual Studio simulation with the specified set of lines
        /// </summary>
        private void Create(bool simulateResharper, params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_vimBuffer);
            _simulation = new VsSimulation(
                _bufferCoordinator,
                simulateResharper: simulateResharper,
                simulateStandardKeyMappings: false,
                editorOperationsFactoryService: EditorOperationsFactoryService);
        }

        /// <summary>
        /// Simple sanity check to ensure that our simulation is working properly
        /// </summary>
        [Fact]
        public void Insert_SanityCheck()
        {
            Create("hello world");
            _textView.MoveCaretTo(0);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _simulation.Run('x');
            Assert.Equal("xhello world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure that S_RETURN will actually come across as such 
        /// </summary>
        [Fact]
        public void KeyMap_ShiftAndReturn()
        {
            Create("cat", "dog");
            _vimBuffer.Process(":map <S-RETURN> o<Esc>", enter: true);
            _simulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Shift));
            Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
            Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            Assert.Equal("", _textBuffer.GetLine(1).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
        }

        /// <summary>
        /// Make sure that S_TAB will actually come across as such 
        /// </summary>
        [Fact]
        public void KeyMap_ShiftAndTab()
        {
            Create("cat", "dog");
            _vimBuffer.Process(":map <S-TAB> o<Esc>", enter: true);
            _simulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Tab, KeyModifiers.Shift));
            Assert.Equal(3, _textBuffer.CurrentSnapshot.LineCount);
            Assert.Equal("cat", _textBuffer.GetLine(0).GetText());
            Assert.Equal("", _textBuffer.GetLine(1).GetText());
            Assert.Equal("dog", _textBuffer.GetLine(2).GetText());
        }

        [Fact]
        public void KeyMap_ShiftAndEnter()
        {
            Create("cat", "dog");
            _vimBuffer.Process(":inoremap <S-CR> <Esc>", enter: true);
            _vimBuffer.Process("i");
            _simulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Enter, KeyModifiers.Shift));
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Regression test for issue #663.  Previously the ; was being seen as part of a possible dead
        /// key combination and mapping wasn't kicking in.  Now that we know it can't be part of a dead
        /// key mapping we process it promptly as it should be 
        /// </summary>
        [Fact]
        public void KeyMap_DoubleSemicolon()
        {
            Create("cat", "dog");
            _vimBuffer.Process(":imap ;; <Esc>", enter: true);
            _vimBuffer.Process("i");
            _simulation.Run(";;");
            Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Make sure that Escape dismisses intellisense even in normal mode
        /// </summary>
        [Fact]
        public void NormalMode_EscapeShouldDismissCompletion()
        {
            Create("cat dog");
            _simulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
            _simulation.DisplayWindowBroker.Setup(x => x.DismissDisplayWindows()).Verifiable();
            _simulation.Run(VimKey.Escape);
            _simulation.DisplayWindowBroker.Verify();
        }

        /// <summary>
        /// Keys like j, k should go to normal mode even when Intellisense is active
        /// </summary>
        [Fact]
        public void NormalMode_CommandKeysGoToVim()
        {
            Create("cat dog");
            _simulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
            _simulation.Run("dw");
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
            _simulation.DisplayWindowBroker.Setup(x => x.IsCompletionActive).Returns(true);
            _simulation.Run(VimKey.Down);
            Assert.False(didProcess);
        }

        /// <summary>
        /// Verify that the back behavior which R# works as expected when we are in 
        /// Insert mode.  It should delete the simple double matched parens
        /// </summary>
        [Fact]
        public void Resharper_Back_ParenWorksInInsert()
        {
            Create(true, "method();", "next");
            _textView.MoveCaretTo(7);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _simulation.Run(VimKey.Back);
            Assert.Equal("method;", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure that back can be used to navigate across an entire line.  Briefly introduced
        /// an issue during the testing of the special casing of Back which caused the key to be
        /// disabled for a time
        /// </summary>
        [Fact]
        public void Reshaprer_Back_AcrossEntireLine()
        {
            Create(true, "hello();", "world");
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.MoveCaretTo(8);
            for (int i = 0; i < 8; i++)
            {
                _simulation.Run(VimKey.Back);
                Assert.Equal(8 - (i + 1), _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Ensure the repeating of the Back command is done properly for Resharper.  We special case
        /// the initial handling of the command.  But this shouldn't affect the repeat as it should
        /// be using CustomProcess under the hood
        /// </summary>
        [Fact]
        public void Resharper_Back_Repeat()
        {
            Create(true, "dog toy", "fish chips");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretToLine(1, 5);
            _simulation.Run(VimKey.Back, VimKey.Escape);
            _textView.MoveCaretTo(4);
            _simulation.Run(".");
            Assert.Equal("dogtoy", _textView.GetLine(0).GetText());
            Assert.Equal(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Without any mappings the Shift+Down should extend the selection downwards and cause us to
        /// enter Visual Mode
        /// </summary>
        [Fact]
        public void StandardCommand_ExtendSelectionDown()
        {
            Create("dog", "cat", "tree");
            _simulation.SimulateStandardKeyMappings = true;
            _simulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Down, KeyModifiers.Shift));
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
            _simulation.SimulateStandardKeyMappings = true;
            _simulation.Run(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Right, KeyModifiers.Shift));
            Assert.Equal(ModeKind.VisualCharacter, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Make sure the Insert key correctly toggles to insert mode then replace
        /// </summary>
        [Fact]
        public void SwitchMode_InsertKey()
        {
            Create(false, "");
            _simulation.Run(VimKey.Insert);
            Assert.Equal(ModeKind.Insert, _vimBuffer.ModeKind);
            _simulation.Run(VimKey.Insert);
            Assert.Equal(ModeKind.Replace, _vimBuffer.ModeKind);
        }

        /// <summary>
        /// Make sure that we allow keys like down to make it directly to Insert mode when there is
        /// an active IWordCompletionSession
        /// </summary>
        [Fact]
        public void WordCompletion_Down()
        {
            Create(false, "c dog", "cat copter");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretTo(1);
            _simulation.Run(KeyNotationUtil.StringToKeyInput("<C-n>"));
            _simulation.Run(KeyNotationUtil.StringToKeyInput("<Down>"));
            Assert.Equal("copter dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// When there is an active IWordCompletionSession we want to let even direct input go directly
        /// to insert mode.  
        /// </summary>
        [Fact]
        public void WordCompletion_TypeChar()
        {
            Create(false, "c dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretTo(1);
            _simulation.Run(KeyNotationUtil.StringToKeyInput("<C-n>"));
            _simulation.Run('s');
            Assert.Equal("cats dog", _textView.GetLine(0).GetText());
            Assert.True(_vimBuffer.InsertMode.ActiveWordCompletionSession.IsNone());
        }
    }
}
