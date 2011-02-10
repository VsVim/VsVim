using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class CommandUtilTest
    {
        private MockRepository _factory;
        private Mock<IVimHost> _vimHost;
        private Mock<IStatusUtil> _statusUtil;
        private ITextViewMotionUtil _motionUtil;
        private ICommonOperations _operations;
        private IRegisterMap _registerMap;
        private IVimData _vimData;
        private ITextView _textView;
        private CommandUtil _commandUtil;
        private ICommandUtil _commandUtilInterface;

        private void Create(params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _vimHost = _factory.Create<IVimHost>();
            _statusUtil = _factory.Create<IStatusUtil>();

            _textView = EditorUtil.CreateView(lines);
            _vimData = new VimData();
            _registerMap = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);

            var localSettings = new LocalSettings(new Vim.GlobalSettings());
            _motionUtil = VimUtil.CreateTextViewMotionUtil(
                _textView,
                settings: localSettings,
                vimData: _vimData);
            _operations = VimUtil.CreateCommonOperations(
                _textView,
                localSettings: localSettings,
                vimHost: _vimHost.Object,
                statusUtil: _statusUtil.Object,
                undoRedoOperations: new UndoRedoOperations(_statusUtil.Object, FSharpOption<ITextUndoHistory>.None, null));
            _commandUtil = new CommandUtil(
                _operations,
                _registerMap,
                _motionUtil,
                _vimData);
            _commandUtilInterface = _commandUtil;
        }

        [Test]
        public void ReplaceChar1()
        {
            Create("foo");
            _commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 1);
            Assert.AreEqual("boo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar2()
        {
            Create("foo");
            _commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('b'), 2);
            Assert.AreEqual("bbo", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar3()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            _commandUtil.ReplaceChar(KeyInputUtil.EnterKey, 1);
            var tss = _textView.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("o", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar4()
        {
            Create("food");
            _textView.MoveCaretTo(1);
            _commandUtil.ReplaceChar(KeyInputUtil.EnterKey, 2);
            var tss = _textView.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("d", tss.GetLineFromLineNumber(1).GetText());
        }

        /// <summary>
        /// Should beep when the count exceeds the buffer length
        ///
        /// Unknown: Should the command still succeed though?  Choosing yes for now but could
        /// certainly be wrong about this.  Thinking yes though because there is no error message
        /// to display
        /// </summary>
        [Test]
        public void ReplaceChar_CountExceedsBufferLength()
        {
            Create("food");
            var tss = _textView.TextSnapshot;
            _vimHost.Setup(x => x.Beep()).Verifiable();
            Assert.IsTrue(_commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('c'), 200).IsCompleted);
            Assert.AreSame(tss, _textView.TextSnapshot);
            _factory.Verify();
        }

        /// <summary>
        /// Cursor should not move as a result of a single ReplaceChar operation
        /// </summary>
        [Test]
        public void ReplaceChar_DontMoveCaret()
        {
            Create("foo");
            Assert.IsTrue(_commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 1).IsCompleted);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Cursor should move for a multiple replace
        /// </summary>
        [Test]
        public void ReplaceChar_MoveCaretForMultiple()
        {
            Create("foo");
            Assert.IsTrue(_commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 2).IsCompleted);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }
    }
}
