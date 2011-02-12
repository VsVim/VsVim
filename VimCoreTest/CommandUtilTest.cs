using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class CommandUtilTest
    {
        private MockRepository _factory;
        private Mock<IVimHost> _vimHost;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ICommonOperations> _operations;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private ITextViewMotionUtil _motionUtil;
        private IRegisterMap _registerMap;
        private IVimData _vimData;
        private IMarkMap _markMap;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private CommandUtil _commandUtil;
        private ICommandUtil _commandUtilInterface;

        private void Create(params string[] lines)
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _vimHost = _factory.Create<IVimHost>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _operations = _factory.Create<ICommonOperations>();
            _operations
                .Setup(x => x.WrapEditInUndoTransaction(It.IsAny<string>(), It.IsAny<FSharpFunc<Unit, Unit>>()))
                .Callback<string, FSharpFunc<Unit, Unit>>((x, y) => y.Invoke(null));

            _textView = EditorUtil.CreateView(lines);
            _textBuffer = _textView.TextBuffer;
            _vimData = new VimData();
            _registerMap = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            _markMap = new MarkMap(new TrackingLineColumnService());
            _globalSettings = new GlobalSettings();
            _localSettings = new LocalSettings(_globalSettings, _textView);

            var localSettings = new LocalSettings(new Vim.GlobalSettings());
            _motionUtil = VimUtil.CreateTextViewMotionUtil(
                _textView,
                settings: localSettings,
                vimData: _vimData);
            _commandUtil = VimUtil.CreateCommandUtil(
                _textView,
                _operations.Object,
                _motionUtil,
                statusUtil: _statusUtil.Object,
                registerMap: _registerMap,
                markMap: _markMap,
                vimData: _vimData);
            _commandUtilInterface = _commandUtil;
        }

        private Register UnnamedRegister
        {
            get { return _registerMap.GetRegister(RegisterName.Unnamed); }
        }

        private void SetLastCommand(NormalCommand command, int? count = null, RegisterName name = null)
        {
            var data = VimUtil.CreateCommandData(count, name);
            var storedCommand = StoredCommand.NewNormalCommand(command, data, CommandFlags.None);
            _vimData.LastCommand = FSharpOption.Create(storedCommand);
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
            _operations.Setup(x => x.Beep()).Verifiable();
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

        [Test]
        public void SetMarkToCaret_StartOfBuffer()
        {
            Create("the cat chased the dog");
            _operations.Setup(x => x.SetMark(_textView.GetCaretPoint(), 'a', _markMap)).Returns(Result.Succeeded).Verifiable();
            _commandUtil.SetMarkToCaret('a');
            _operations.Verify();
        }

        /// <summary>
        /// Beep and pass the error message onto IStatusUtil if there is na error
        /// </summary>
        [Test]
        public void SetMarkToCaret_BeepOnFailure()
        {
            Create("the cat chased the dog");
            _operations.Setup(x => x.SetMark(_textView.GetCaretPoint(), 'a', _markMap)).Returns(Result.NewFailed("e")).Verifiable();
            _operations.Setup(x => x.Beep()).Verifiable();
            _statusUtil.Setup(x => x.OnError("e")).Verifiable();
            _commandUtil.SetMarkToCaret('a');
            _factory.Verify();
        }

        [Test]
        public void JumpToMark_Simple()
        {
            Create("the cat chased the dog");
            _operations.Setup(x => x.JumpToMark('a', _markMap)).Returns(Result.Succeeded).Verifiable();
            _commandUtil.JumpToMark('a');
            _operations.Verify();
        }

        /// <summary>
        /// Pass the error message onto IStatusUtil if there is na error
        /// </summary>
        [Test]
        public void JumpToMark_OnFailure()
        {
            Create("the cat chased the dog");
            _operations.Setup(x => x.JumpToMark('a', _markMap)).Returns(Result.NewFailed("e")).Verifiable();
            _statusUtil.Setup(x => x.OnError("e")).Verifiable();
            _commandUtil.JumpToMark('a');
            _factory.Verify();
        }

        /// <summary>
        /// If there is no command to repeat then just beep
        /// </summary>
        [Test]
        public void RepeatLastCommand_NoCommandToRepeat()
        {
            Create("foo");
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            _factory.Verify();
        }

        /// <summary>
        /// Repeat a simple text insert
        /// </summary>
        [Test]
        public void RepeatLastCommand_InsertText()
        {
            Create("");
            _vimData.LastCommand = FSharpOption.Create(StoredCommand.NewTextChangeCommand(TextChange.NewInsert("h")));
            _operations.Setup(x => x.InsertText("h", 1)).Verifiable();
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            _factory.Verify();
        }

        /// <summary>
        /// Repeat a simple text insert with a new count
        /// </summary>
        [Test]
        public void RepeatLastCommand_InsertTextNewCount()
        {
            Create("");
            _vimData.LastCommand = FSharpOption.Create(StoredCommand.NewTextChangeCommand(TextChange.NewInsert("h")));
            _operations.Setup(x => x.InsertText("h", 3)).Verifiable();
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData(count: 3));
            _factory.Verify();
        }

        /// <summary>
        /// Repeat a simple command
        /// </summary>
        [Test]
        public void RepeatLastCommand_SimpleCommand()
        {
            Create("");
            var didRun = false;
            SetLastCommand(VimUtil.CreatePing(data =>
            {
                Assert.IsTrue(data.Count.IsNone());
                Assert.IsTrue(data.RegisterName.IsNone());
                didRun = true;
            }));

            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Repeat a simple command but give it a new count.  This should override the previous
        /// count
        /// </summary>
        [Test]
        public void RepeatLastCommand_SimpleCommandNewCount()
        {
            Create("");
            var didRun = false;
            SetLastCommand(VimUtil.CreatePing(data =>
            {
                Assert.IsTrue(data.Count.IsSome(2));
                Assert.IsTrue(data.RegisterName.IsNone());
                didRun = true;
            }));

            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData(count: 2));
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Repeating a command should not clear the last command
        /// </summary>
        [Test]
        public void RepeatLastCommand_DontClearPrevious()
        {
            Create("");
            var didRun = false;
            var command = VimUtil.CreatePing(data =>
            {
                Assert.IsTrue(data.Count.IsNone());
                Assert.IsTrue(data.RegisterName.IsNone());
                didRun = true;
            });
            SetLastCommand(command);
            var saved = _vimData.LastCommand.Value;
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            Assert.AreEqual(saved, _vimData.LastCommand.Value);
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Guard against the possiblitity of creating a StackOverflow by having the repeat
        /// last command recursively call itself
        /// </summary>
        [Test]
        public void RepeatLastCommand_GuardAgainstStacOverflow()
        {
            var didRun = false;
            SetLastCommand(VimUtil.CreatePing(data =>
            {
                didRun = true;
                _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            }));

            _statusUtil.Setup(x => x.OnError(Resources.NormalMode_RecursiveRepeatDetected)).Verifiable();
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData());
            _factory.Verify();
            Assert.IsTrue(didRun);
        }

        /// <summary>
        /// Pass a barrage of spans and verify they map back and forth within the same 
        /// ITextBuffer
        /// </summary>
        [Test]
        public void CalculateVisualSpan_CharacterBackAndForth()
        {
            Create("the dog kicked the ball", "into the tree");

            Action<SnapshotSpan> action = span =>
            {
                var visual = VisualSpan.NewCharacter(span);
                var stored = StoredVisualSpan.OfVisualSpan(visual);
                var restored = _commandUtil.CalculateVisualSpan(stored);
                Assert.AreEqual(visual, restored);
            };

            action(new SnapshotSpan(_textView.TextSnapshot, 0, 3));
            action(new SnapshotSpan(_textView.TextSnapshot, 0, 4));
            action(new SnapshotSpan(_textView.GetLine(0).Start, _textView.GetLine(1).Start.Add(1)));
        }

        /// <summary>
        /// When repeating a multi-line characterwise span where the caret moves left,
        /// we need to use the caret to the end of the line on the first line
        /// </summary>
        [Test]
        public void CalculateVisualSpan_CharacterMultilineMoveCaretLeft()
        {
            Create("the dog", "ball");

            var span = new SnapshotSpan(_textView.GetPoint(3), _textView.GetLine(1).Start.Add(1));
            var stored = StoredVisualSpan.OfVisualSpan(VisualSpan.NewCharacter(span));
            _textView.MoveCaretTo(1);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            var expected = new SnapshotSpan(_textView.GetPoint(1), _textView.GetLine(1).Start.Add(1));
            Assert.AreEqual(expected, restored.AsCharacter().Item);
        }

        /// <summary>
        /// When restoring for a single line maintain the length but do it from the caret
        /// point and not the original
        /// </summary>
        [Test]
        public void CalculateVisualSpan_CharacterSingleLine()
        {
            Create("the dog kicked the cat", "and ball");

            var span = new SnapshotSpan(_textView.TextSnapshot, 3, 4);
            var stored = StoredVisualSpan.OfVisualSpan(VisualSpan.NewCharacter(span));
            _textView.MoveCaretTo(1);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            var expected = new SnapshotSpan(_textView.GetPoint(1), 4);
            Assert.AreEqual(expected, restored.AsCharacter().Item);
        }

        /// <summary>
        /// Restore a Linewise span from the same offset
        /// </summary>
        [Test]
        public void CalculateVisualSpan_Linewise()
        {
            Create("a", "b", "c", "d");
            var span = VisualSpan.NewLine(_textView.GetLineRange(0, 1));
            var stored = StoredVisualSpan.OfVisualSpan(span);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            Assert.AreEqual(span, restored);
        }

        /// <summary>
        /// Restore a Linewise span from a different offset
        /// </summary>
        [Test]
        public void CalculateVisualSpan_LinewiseDifferentOffset()
        {
            Create("a", "b", "c", "d");
            var span = VisualSpan.NewLine(_textView.GetLineRange(0, 1));
            var stored = StoredVisualSpan.OfVisualSpan(span);
            _textView.MoveCaretToLine(1);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            Assert.AreEqual(_textView.GetLineRange(1, 2), restored.AsLine().Item);
        }

        /// <summary>
        /// Restore a Linewise span from a different offset which causes the count
        /// to be invalid
        /// </summary>
        [Test]
        public void CalculateVisualSpan_LinewiseCountPastEndOfBuffer()
        {
            Create("a", "b", "c", "d");
            var span = VisualSpan.NewLine(_textView.GetLineRange(0, 2));
            var stored = StoredVisualSpan.OfVisualSpan(span);
            _textView.MoveCaretToLine(3);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            Assert.AreEqual(_textView.GetLineRange(3, 3), restored.AsLine().Item);
        }

        /// <summary>
        /// Restore of Block span at the same offset.  
        /// </summary>
        [Test]
        public void CalculateVisualSpan_Block()
        {
            Create("the", "dog", "kicked", "the", "ball");

            var col = new NormalizedSnapshotSpanCollection(
                new[]
                {
                    _textView.GetLineSpan(0, 1),
                    _textView.GetLineSpan(1, 1)
                });
            var span = VisualSpan.NewBlock(col);
            var stored = StoredVisualSpan.OfVisualSpan(span);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            CollectionAssert.AreEquivalent(col, restored.AsBlock().Item);
        }

        /// <summary>
        /// Restore of Block span at one character to the right
        /// </summary>
        [Test]
        public void CalculateVisualSpan_BlockOneCharecterRight()
        {
            Create("the", "dog", "kicked", "the", "ball");

            var col = new NormalizedSnapshotSpanCollection(
                new[]
                {
                    _textView.GetLineSpan(0, 1),
                    _textView.GetLineSpan(1, 1)
                });
            var span = VisualSpan.NewBlock(col);
            var stored = StoredVisualSpan.OfVisualSpan(span);
            _textView.MoveCaretTo(1);
            var restored = _commandUtil.CalculateVisualSpan(stored);
            CollectionAssert.AreEquivalent(
                new[]
                {
                    _textView.GetLineSpan(0, 1, 1),
                    _textView.GetLineSpan(1, 1, 1)
                },
                restored.AsBlock().Item);
        }

        [Test]
        public void DeleteCharacterAtCursor_Simple()
        {
            Create("foo", "bar");
            _commandUtil.DeleteCharacterAtCursor(1, UnnamedRegister, ModeSwitch.NoSwitch);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete several characters
        /// </summary>
        [Test]
        public void DeleteCharacterAtCursor_TwoCharacters()
        {
            Create("foo", "bar");
            _commandUtil.DeleteCharacterAtCursor(2, UnnamedRegister, ModeSwitch.NoSwitch);
            Assert.AreEqual("o", _textView.GetLine(0).GetText());
            Assert.AreEqual("fo", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Delete at a different offset and make sure the cursor is positioned correctly
        /// </summary>
        [Test]
        public void DeleteCharacterAtCursor_NonZeroOffset()
        {
            Create("the cat", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterAtCursor(2, UnnamedRegister, ModeSwitch.NoSwitch);
            Assert.AreEqual("t cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("he", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When the count exceeds the length of the line it should delete to the end of the 
        /// line
        /// </summary>
        [Test]
        public void DeleteCharacterAtCursor_CountExceedsLine()
        {
            Create("the cat", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterAtCursor(300, UnnamedRegister, ModeSwitch.NoSwitch);
            Assert.AreEqual("t", _textView.GetLine(0).GetText());
            Assert.AreEqual("he cat", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteCharacterBeforeCursor_Simple()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterBeforeCursor(1, UnnamedRegister);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When the count exceeds the line just delete to the start of the line
        /// </summary>
        [Test]
        public void DeleteCharacterBeforeCursor_CountExceedsLine()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterBeforeCursor(300, UnnamedRegister);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak_Simple()
        {
            Create("foo", "bar", "baz", "jaz");
            _commandUtil.DeleteLines(1, UnnamedRegister);
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteLinesIncludingLineBreak_WithCount()
        {
            Create("foo", "bar", "baz", "jaz");
            _commandUtil.DeleteLines(2, UnnamedRegister);
            Assert.AreEqual("baz", _textView.GetLine(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete the last line and make sure it actually deletes a line from the buffer
        /// </summary>
        [Test]
        public void DeleteLinesIncludingLineBreak_LastLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretToLine(1);
            _commandUtil.DeleteLines(1, UnnamedRegister);
            Assert.AreEqual("bar" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual("foo", _textView.GetLine(0).GetText());
        }
    }
}
