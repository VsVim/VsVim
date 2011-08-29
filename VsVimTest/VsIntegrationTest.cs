using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
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
    [TestFixture]
    public sealed class VsIntegrationTest : VimTestBase
    {
        private VsSimulation _simulation;
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
            _vimBuffer = Vim.CreateVimBuffer(_textView);
            _bufferCoordinator = new VimBufferCoordinator(_vimBuffer);
            _simulation = new VsSimulation(_bufferCoordinator, simulateResharper);
        }

        /// <summary>
        /// Simple sanity check to ensure that our simulation is working properly
        /// </summary>
        [Test]
        public void Insert_SanityCheck()
        {
            Create("hello world");
            _textView.MoveCaretTo(0);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _simulation.Run('x');
            Assert.AreEqual("xhello world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Verify that the back behavior which R# works as expected when we are in 
        /// Insert mode.  It should delete the simple double matched parens
        /// </summary>
        [Test]
        public void Resharper_Back_ParenWorksInInsert()
        {
            Create(true, "method();", "next");
            _textView.MoveCaretTo(7);
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _simulation.Run(VimKey.Back);
            Assert.AreEqual("method;", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure that back can be used to navigate across an entire line.  Briefly introduced
        /// an issue during the testing of the special casing of Back which caused the key to be
        /// disabled for a time
        /// </summary>
        [Test]
        public void Reshaprer_Back_AcrossEntireLine()
        {
            Create(true, "hello();", "world");
            _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
            _textView.MoveCaretTo(8);
            for (int i = 0; i < 8; i++)
            {
                _simulation.Run(VimKey.Back);
                Assert.AreEqual(8 - (i + 1), _textView.GetCaretPoint().Position);
            }
        }

        /// <summary>
        /// Ensure the repeating of the Back command is done properly for Resharper.  We special case
        /// the initial handling of the command.  But this shouldn't affect the repeat as it should
        /// be using CustomProcess under the hood
        /// </summary>
        [Test]
        public void Resharper_Back_Repeat()
        {
            Create(true, "dog toy", "fish chips");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretToLine(1, 5);
            _vimBuffer.Process(VimKey.Back, VimKey.Escape);
            _textView.MoveCaretTo(4);
            _vimBuffer.Process(".");
            Assert.AreEqual("dogtoy", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure that we allow keys like down to make it directly to Insert mode when there is
        /// an active IWordCompletionSession
        /// </summary>
        [Test]
        public void WordCompletion_Down()
        {
            Create(false, "c dog", "cat copter");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretTo(1);
            _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<Down>"));
            Assert.AreEqual("copter dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// When there is an active IWordCompletionSession we want to let even direct input go directly
        /// to insert mode.  
        /// </summary>
        [Test]
        public void WordCompletion_TypeChar()
        {
            Create(false, "c dog", "cat");
            _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _textView.MoveCaretTo(1);
            _vimBuffer.Process(KeyNotationUtil.StringToKeyInput("<C-N>"));
            _vimBuffer.Process('s');
            Assert.AreEqual("cats dog", _textView.GetLine(0).GetText());
            Assert.IsTrue(_vimBuffer.InsertMode.ActiveWordCompletionSession.IsNone());
        }
    }
}
