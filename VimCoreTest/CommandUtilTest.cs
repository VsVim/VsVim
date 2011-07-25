using System;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
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
        private Mock<IMacroRecorder> _recorder;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ICommonOperations> _operations;
        private Mock<ISmartIndentationService> _smartIdentationService;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private IMotionUtil _motionUtil;
        private IRegisterMap _registerMap;
        private IVimData _vimData;
        private IMarkMap _markMap;
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private IJumpList _jumpList;
        private IFoldManager _foldManager;
        private CommandUtil _commandUtil;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _textBuffer = _textView.TextBuffer;
            _foldManager = EditorUtil.FactoryService.FoldManagerFactory.GetFoldManager(_textView);

            _factory = new MockRepository(MockBehavior.Loose);
            _vimHost = _factory.Create<IVimHost>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _recorder = _factory.Create<IMacroRecorder>(MockBehavior.Loose);
            _smartIdentationService = _factory.Create<ISmartIndentationService>();

            _vimData = new VimData();
            _registerMap = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice().Object);
            _markMap = new MarkMap(new TrackingLineColumnService());
            _globalSettings = new GlobalSettings();
            _localSettings = new LocalSettings(_globalSettings, EditorUtil.GetEditorOptions(_textView), _textView);

            _operations = _factory.Create<ICommonOperations>();
            _operations.Setup(x => x.EnsureCaretOnScreenAndTextExpanded());
            _operations.Setup(x => x.RaiseSearchResultMessage(It.IsAny<SearchResult>()));
            _operations.Setup(x => x.EditorOptions).Returns(EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(_textView));
            _operations
                .Setup(x => x.MoveCaretToPointAndCheckVirtualSpace(It.IsAny<SnapshotPoint>()))
                .Callback<SnapshotPoint>(
                    point =>
                    {
                        TextViewUtil.MoveCaretToPoint(_textView, point);
                        CommonUtil.MoveCaretForVirtualEdit(_textView, _globalSettings);
                    });

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
                localSettings: _localSettings,
                registerMap: _registerMap,
                markMap: _markMap,
                vimData: _vimData,
                foldManager: _foldManager,
                smartIndentationService: _smartIdentationService.Object,
                recorder: _recorder.Object);
            _jumpList = _commandUtil._jumpList;
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

        private void AssertInsertWithTransaction(CommandResult result)
        {
            Assert.IsTrue(result.IsCompleted);
            var modeSwitch = result.AsCompleted().Item;
            Assert.IsTrue(modeSwitch.IsSwitchModeWithArgument);
            var data = modeSwitch.AsSwitchModeWithArgument();
            Assert.AreEqual(ModeKind.Insert, data.Item1);
            Assert.IsTrue(data.Item2.IsInsertWithTransaction);
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
        /// Caret should not move as a result of a single ReplaceChar operation
        /// </summary>
        [Test]
        public void ReplaceChar_DontMoveCaret()
        {
            Create("foo");
            Assert.IsTrue(_commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 1).IsCompleted);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should move for a multiple replace
        /// </summary>
        [Test]
        public void ReplaceChar_MoveCaretForMultiple()
        {
            Create("foo");
            Assert.IsTrue(_commandUtil.ReplaceChar(KeyInputUtil.CharToKeyInput('u'), 2).IsCompleted);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Should be beeping at the last line in the ITextBuffer
        /// </summary>
        [Test]
        public void ScrollLines_Down_BeepAtLastLine()
        {
            Create("dog", "cat");
            _textView.MoveCaretToLine(1);
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.ScrollLines(ScrollDirection.Down, true, FSharpOption<int>.None);
            _operations.Verify();
        }

        /// <summary>
        /// Make sure the scroll lines down will hit the bottom of the screen
        /// </summary>
        [Test]
        public void ScrollLines_Down_ToBottom()
        {
            Create("a", "b", "c", "d");
            for (var i = 0; i < 5; i++)
            {
                _commandUtil.ScrollLines(ScrollDirection.Down, true, FSharpOption<int>.None);
            }
            Assert.AreEqual(_textView.GetLine(3).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Should be beeping at the first line in the ITextBuffer
        /// </summary>
        [Test]
        public void ScrollLines_Up_BeepAtFirstLine()
        {
            Create("dog", "cat");
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.ScrollLines(ScrollDirection.Up, true, FSharpOption<int>.None);
            _operations.Verify();
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
            var change = TextChange.NewInsert("h");
            _vimData.LastCommand = FSharpOption.Create(StoredCommand.NewTextChangeCommand(change));
            _operations.Setup(x => x.ApplyTextChange(change, false, 1)).Verifiable();
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
            var change = TextChange.NewInsert("h");
            _vimData.LastCommand = FSharpOption.Create(StoredCommand.NewTextChangeCommand(change));
            _operations.Setup(x => x.ApplyTextChange(change, false, 3)).Verifiable();
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
        /// When dealing with a repeat of a linked command where a new count is provided, only
        /// the first command gets the new count.  The linked command gets the original count
        /// </summary>
        [Test]
        public void RepeatLastCommand_OnlyFirstCommandGetsNewCount()
        {
            Create("");
            var didRun1 = false;
            var didRun2 = false;
            var command1 = VimUtil.CreatePing(
                data =>
                {
                    didRun1 = true;
                    Assert.AreEqual(2, data.CountOrDefault);
                });
            var command2 = VimUtil.CreatePing(
                data =>
                {
                    didRun2 = true;
                    Assert.AreEqual(1, data.CountOrDefault);
                });
            var command = StoredCommand.NewLinkedCommand(
                StoredCommand.NewNormalCommand(command1, VimUtil.CreateCommandData(), CommandFlags.None),
                StoredCommand.NewNormalCommand(command2, VimUtil.CreateCommandData(), CommandFlags.None));
            _vimData.LastCommand = FSharpOption.Create(command);
            _commandUtil.RepeatLastCommand(VimUtil.CreateCommandData(count: 2));
            Assert.IsTrue(didRun1 && didRun2);
        }

        /// <summary>
        /// When repeating commands the mode should not be switched as if the last command ran
        /// but instead should remain in the current Normal mode.  This is best illustrated by 
        /// trying to repeat the 'o' command
        /// </summary>
        [Test]
        public void RepeatLastCommand_DontSwitchModes()
        {
            var command1 = VimUtil.CreatePing(
                data =>
                {

                });
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

            var col = _textView.GetBlock(0, 1, 0, 2);
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

            var col = _textView.GetBlock(0, 1, 0, 2);
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
        public void DeleteCharacterAtCaret_Simple()
        {
            Create("foo", "bar");
            _commandUtil.DeleteCharacterAtCaret(1, UnnamedRegister);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete several characters
        /// </summary>
        [Test]
        public void DeleteCharacterAtCaret_TwoCharacters()
        {
            Create("foo", "bar");
            _commandUtil.DeleteCharacterAtCaret(2, UnnamedRegister);
            Assert.AreEqual("o", _textView.GetLine(0).GetText());
            Assert.AreEqual("fo", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Delete at a different offset and make sure the cursor is positioned correctly
        /// </summary>
        [Test]
        public void DeleteCharacterAtCaret_NonZeroOffset()
        {
            Create("the cat", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterAtCaret(2, UnnamedRegister);
            Assert.AreEqual("t cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("he", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When the count exceeds the length of the line it should delete to the end of the 
        /// line
        /// </summary>
        [Test]
        public void DeleteCharacterAtCaret_CountExceedsLine()
        {
            Create("the cat", "bar");
            _globalSettings.VirtualEdit = "onemore";
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterAtCaret(300, UnnamedRegister);
            Assert.AreEqual("t", _textView.GetLine(0).GetText());
            Assert.AreEqual("he cat", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteCharacterBeforeCaret_Simple()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterBeforeCaret(1, UnnamedRegister);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When the count exceeds the line just delete to the start of the line
        /// </summary>
        [Test]
        public void DeleteCharacterBeforeCaret_CountExceedsLine()
        {
            Create("foo");
            _textView.MoveCaretTo(1);
            _commandUtil.DeleteCharacterBeforeCaret(300, UnnamedRegister);
            Assert.AreEqual("oo", _textView.GetLine(0).GetText());
            Assert.AreEqual("f", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Standard case of deleting several lines in the buffer
        /// </summary>
        [Test]
        public void DeleteLines_Multiple()
        {
            Create("cat", "dog", "bear");
            _commandUtil.DeleteLines(2, UnnamedRegister);
            Assert.AreEqual(EditorUtil.CreateLinesWithLineBreak("cat", "dog"), UnnamedRegister.StringValue);
            Assert.AreEqual("bear", _textView.GetLine(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Verify the deleting of lines where the count causes the deletion to cross 
        /// over a fold
        /// </summary>
        [Test]
        public void DeleteLines_OverFold()
        {
            Create("cat", "dog", "bear", "fish", "tree");
            _foldManager.CreateFold(_textView.GetLineRange(1, 2));
            _commandUtil.DeleteLines(3, UnnamedRegister);
            Assert.AreEqual(EditorUtil.CreateLinesWithLineBreak("cat", "dog", "bear", "fish"), UnnamedRegister.StringValue);
            Assert.AreEqual("tree", _textView.GetLine(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Verify the deleting of lines where the count causes the deletion to cross 
        /// over a fold which begins the deletion span
        /// </summary>
        [Test]
        public void DeleteLines_StartOfFold()
        {
            Create("cat", "dog", "bear", "fish", "tree");
            _foldManager.CreateFold(_textView.GetLineRange(0, 1));
            _commandUtil.DeleteLines(2, UnnamedRegister);
            Assert.AreEqual(EditorUtil.CreateLinesWithLineBreak("cat", "dog", "bear"), UnnamedRegister.StringValue);
            Assert.AreEqual("fish", _textView.GetLine(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        [Test]
        public void DeleteLines_Simple()
        {
            Create("foo", "bar", "baz", "jaz");
            _commandUtil.DeleteLines(1, UnnamedRegister);
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void DeleteLines_WithCount()
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
        public void DeleteLines_LastLine()
        {
            Create("foo", "bar");
            _textView.MoveCaretToLine(1);
            _commandUtil.DeleteLines(1, UnnamedRegister);
            Assert.AreEqual("bar" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            Assert.AreEqual("foo", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Caret should be moved to the start of the shift
        /// </summary>
        [Test]
        public void ShiftLinesRightVisual_BlockShouldPutCaretAtStart()
        {
            Create("cat", "dog");
            _textView.MoveCaretToLine(1);
            var span = _textView.GetVisualSpanBlock(column: 1, length: 2, startLine: 0, lineCount: 2);
            _operations
                .Setup(x => x.ShiftLineBlockRight(span.AsBlock().item, 1))
                .Callback(() => _textView.SetText("c  at", "d  og"))
                .Verifiable();
            _commandUtil.ShiftLinesRightVisual(1, span);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be moved to the start of the shift
        /// </summary>
        [Test]
        public void ShiftLinesLeftVisual_BlockShouldPutCaretAtStart()
        {
            Create("c  at", "d  og");
            _textView.MoveCaretToLine(1);
            var span = _textView.GetVisualSpanBlock(column: 1, length: 1, startLine: 0, lineCount: 2);
            _operations
                .Setup(x => x.ShiftLineBlockRight(span.AsBlock().item, 1))
                .Callback(() => _textView.SetText("cat", "dog"))
                .Verifiable();
            _commandUtil.ShiftLinesLeftVisual(1, span);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Changing a word based motion forward should not delete trailing whitespace
        /// </summary>
        [Test]
        public void ChangeMotion_WordSpan()
        {
            Create("foo  bar");
            _commandUtil.ChangeMotion(
                UnnamedRegister,
                VimUtil.CreateMotionResult(
                    _textBuffer.GetSpan(0, 3),
                    isForward: true,
                    motionKind: MotionKind.CharacterWiseExclusive,
                    flags: MotionResultFlags.AnyWord));
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("foo", UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Changing a word based motion forward should not delete trailing whitespace
        /// </summary>
        [Test]
        public void ChangeMotion_WordShouldSaveTrailingWhitespace()
        {
            Create("foo  bar");
            _commandUtil.ChangeMotion(
                UnnamedRegister,
                VimUtil.CreateMotionResult(
                    _textBuffer.GetSpan(0, 5),
                    isForward: true,
                    motionKind: MotionKind.NewLineWise(CaretColumn.None),
                    flags: MotionResultFlags.AnyWord));
            Assert.AreEqual("  bar", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual("foo", UnnamedRegister.StringValue);
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete trailing whitespace in a non-word motion
        /// </summary>
        [Test]
        public void ChangeMotion_NonWordShouldDeleteTrailingWhitespace()
        {
            Create("foo  bar");
            _commandUtil.ChangeMotion(
                UnnamedRegister,
                VimUtil.CreateMotionResult(
                    _textBuffer.GetSpan(0, 5),
                    isForward: true,
                    motionKind: MotionKind.NewLineWise(CaretColumn.None)));
            Assert.AreEqual("bar", _textBuffer.GetLineRange(0).GetText());
        }

        /// <summary>
        /// Leave whitespace in a backward word motion
        /// </summary>
        [Test]
        public void ChangeMotion_LeaveWhitespaceIfBackward()
        {
            Create("cat dog tree");
            _commandUtil.ChangeMotion(
                UnnamedRegister,
                VimUtil.CreateMotionResult(
                    _textBuffer.GetSpan(4, 4),
                    false,
                    MotionKind.CharacterWiseInclusive));
            Assert.AreEqual("cat tree", _textBuffer.GetLineRange(0).GetText());
            Assert.AreEqual(4, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be positioned at the end of the first line
        /// </summary>
        [Test]
        public void JoinLines_Caret()
        {
            Create("dog", "cat", "bear");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Callback(() => _textView.SetText("dog cat", "bear"))
                .Verifiable();
            _commandUtil.JoinLines(JoinKind.RemoveEmptySpaces, 1);
            _operations.Verify();
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Should beep when the count specified causes the range to exceed the 
        /// length of the ITextBuffer
        /// </summary>
        [Test]
        public void JoinLines_CountExceedsBuffer()
        {
            Create("dog", "cat", "bear");
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.JoinLines(JoinKind.RemoveEmptySpaces, 3000);
            _operations.Verify();
        }

        /// <summary>
        /// A count of 2 is the same as 1 for JoinLines
        /// </summary>
        [Test]
        public void JoinLines_CountOfTwoIsSameAsOne()
        {
            Create("dog", "cat", "bear");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Callback(() => _textView.SetText("dog cat", "bear"))
                .Verifiable();
            _commandUtil.JoinLines(JoinKind.RemoveEmptySpaces, 2);
            _operations.Verify();
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// The caret behavior for the 'J' family of commands is hard to follow at first
        /// but comes down to a very simple behavior.  The caret should be placed 1 past
        /// the last character in the second to last line joined
        /// </summary>
        [Test]
        public void JoinLines_CaretWithBlankAtEnd()
        {
            Create("a ", "b", "c");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces))
                .Callback(() => _textView.SetText("a b c"))
                .Verifiable();
            _commandUtil.JoinLines(JoinKind.RemoveEmptySpaces, 3);
            _operations.Verify();
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeCaseCaretPoint_Simple()
        {
            Create("bar", "baz");
            _commandUtil.ChangeCaseCaretPoint(ChangeCharacterKind.ToUpperCase, 1);
            Assert.AreEqual("Bar", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeCaseCaretPoint_WithCount()
        {
            Create("bar", "baz");
            _commandUtil.ChangeCaseCaretPoint(ChangeCharacterKind.ToUpperCase, 2);
            Assert.AreEqual("BAr", _textView.GetLineRange(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// If the count exceeds the line then just do the rest of the line
        /// </summary>
        [Test]
        public void ChangeCaseCaretPoint_CountExceedsLine()
        {
            Create("bar", "baz");
            _globalSettings.VirtualEdit = "onemore";
            _commandUtil.ChangeCaseCaretPoint(ChangeCharacterKind.ToUpperCase, 300);
            Assert.AreEqual("BAR", _textView.GetLine(0).GetText());
            Assert.AreEqual("baz", _textView.GetLine(1).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        [Test]
        public void ChangeCaseCaretLine_Simple()
        {
            Create("foo", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.ChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase);
            Assert.AreEqual("FOO", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the caret moves past the whitespace when changing case
        /// </summary>
        [Test]
        public void ChangeCaseCaretLine_WhiteSpaceStart()
        {
            Create("  foo", "bar");
            _textView.MoveCaretTo(4);
            _commandUtil.ChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase);
            Assert.AreEqual("  FOO", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Don't change anything but letters
        /// </summary>
        [Test]
        public void ChangeCaseCaretLine_ExcludeNumbers()
        {
            Create("foo123", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.ChangeCaseCaretLine(ChangeCharacterKind.ToUpperCase);
            Assert.AreEqual("FOO123", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Change the caret line with the rot13 encoding
        /// </summary>
        [Test]
        public void ChangeCaseCaretLine_Rot13()
        {
            Create("hello", "bar");
            _textView.MoveCaretTo(1);
            _commandUtil.ChangeCaseCaretLine(ChangeCharacterKind.Rot13);
            Assert.AreEqual("uryyb", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// An invalid motion should produce an error and not call the passed in function
        /// </summary>
        [Test]
        public void RunWithMotion_InvalidMotionShouldError()
        {
            Create("");
            var data = VimUtil.CreateMotionData(Motion.NewMark('a'));
            Func<MotionResult, CommandResult> func =
                _ =>
                {
                    Assert.Fail("Should not run");
                    return null;
                };
            var result = _commandUtil.RunWithMotion(data, func.ToFSharpFunc());
            Assert.IsTrue(result.IsError);
        }

        /// <summary>
        /// Do a put operation on an empty line and ensure we don't accidentaly move off 
        /// of the end of the line and insert the text in the middle of the line break
        /// </summary>
        [Test]
        public void PutAfter_EmptyLine()
        {
            Create("", "dog");
            UnnamedRegister.UpdateValue("pig", OperationKind.CharacterWise);
            _operations
                .Setup(x => x.Put(_textView.GetPoint(0), It.IsAny<StringData>(), OperationKind.CharacterWise))
                .Callback(() => _textView.SetText("pig", "dog"))
                .Verifiable();
            _commandUtil.PutAfterCaret(UnnamedRegister, 1, false);
            _operations.Verify();
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// PutAfter with a block value should position the cursor on the first character
        /// of the first string in the block
        /// </summary>
        [Test]
        public void PutAfter_Block()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _operations.SetupPut(_textBuffer, "daaog", "cbbat");
            _commandUtil.PutAfterCaret(UnnamedRegister, 1, false);
            _operations.Verify();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// PutAfter with a block value with the moveCaret option should put the caret after
        /// the last inserted text from the last item in the block
        /// </summary>
        [Test]
        public void PutAfter_Block_WithMoveCaret()
        {
            Create("dog", "cat");
            UnnamedRegister.UpdateBlockValues("aa", "bb");
            _operations.SetupPut(_textBuffer, "daaog", "cbbat");
            _commandUtil.PutAfterCaret(UnnamedRegister, 1, moveCaretAfterText: true);
            _operations.Verify();
            Assert.AreEqual(_textView.GetLine(1).Start.Add(3), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Do a put before operation on an empty line and ensure we don't accidentally
        /// move up to the previous line break and insert there
        /// </summary>
        [Test]
        public void PutBefore_EmptyLine()
        {
            Create("dog", "", "cat");
            UnnamedRegister.UpdateValue("pig", OperationKind.CharacterWise);
            _operations
                .Setup(x => x.Put(_textView.GetLine(1).Start, It.IsAny<StringData>(), OperationKind.CharacterWise))
                .Callback(() => _textView.SetText("pig", "dog"))
                .Verifiable();
            _textView.MoveCaretToLine(1);
            _commandUtil.PutBeforeCaret(UnnamedRegister, 1, false);
            _operations.Verify();
            Assert.AreEqual(_textView.GetLine(1).Start.Add(2), _textView.GetCaretPoint());
        }

        /// <summary>
        /// Replace the text and put the caret at the end of the selection
        /// </summary>
        [Test]
        public void PutOverSelection_Character()
        {
            Create("hello world");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 0, 5));
            UnnamedRegister.UpdateValue("dog");
            _operations.SetupPut(_textBuffer, "dog world");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: false, visualSpan: visualSpan);
            Assert.AreEqual("dog world", _textView.GetLine(0).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Replace the text and put the caret after the selection span
        /// </summary>
        [Test]
        public void PutOverSelection_Character_WithCaretMove()
        {
            Create("hello world");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 0, 5));
            UnnamedRegister.UpdateValue("dog");
            _operations.SetupPut(_textBuffer, "dog world");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: true, visualSpan: visualSpan);
            Assert.AreEqual("dog world", _textView.GetLine(0).GetText());
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Put a linewise paste over a character visual span.  Make sure that we 
        /// put the appropriate text and OperationKind into the source register
        /// </summary>
        [Test]
        public void PutOverSelection_Character_WithLine()
        {
            Create("dog");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 1, 1));
            UnnamedRegister.UpdateValue("pig", OperationKind.LineWise);
            _operations.SetupPut(_textBuffer, "d", "pig", "g");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: false, visualSpan: visualSpan);
            Assert.AreEqual("o", UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Make sure it removes both lines and inserts the text at the start 
        /// of the line range span.  Should position the caret at the start as well
        /// </summary>
        [Test]
        public void PutOverSelection_Line()
        {
            Create("the cat", "chased", "the dog");
            var visualSpan = VisualSpan.NewLine(_textView.GetLineRange(0, 1));
            UnnamedRegister.UpdateValue("dog");
            _operations.SetupPut(_textBuffer, "dog", "the dog");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: false, visualSpan: visualSpan);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("the dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Caret should be moved to the start of the next line if the 'moveCaretAfterText' 
        /// option is specified
        /// </summary>
        [Test]
        public void PutOverSelection_Line_WithCaretMove()
        {
            Create("the cat", "chased", "the dog");
            var visualSpan = VisualSpan.NewLine(_textView.GetLineRange(0, 1));
            UnnamedRegister.UpdateValue("dog");
            _operations.SetupPut(_textBuffer, "dog", "the dog");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: true, visualSpan: visualSpan);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual("the dog", _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Ensure that we have the correct OperationKind for a put over a line selection.  It
        /// should be LineWise even if the put source is CharacterWise
        /// </summary>
        [Test]
        public void PutOverSelection_Line_WithCharacter()
        {
            Create("dog", "cat");
            var visualSpan = VisualSpan.NewLine(_textView.GetLineRange(0));
            UnnamedRegister.UpdateValue("pig");
            _operations.SetupPut(_textBuffer, "pig", "cat");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: false, visualSpan: visualSpan);
            Assert.AreEqual("dog" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Make sure caret is positioned on the last inserted character on the first
        /// inserted line
        /// </summary>
        [Test]
        public void PutOverSelection_Block()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlock(1, 1, 0, 2));
            UnnamedRegister.UpdateValue("z");
            _operations.SetupPut(_textBuffer, "czt", "dg");
            _commandUtil.PutOverSelection(UnnamedRegister, 1, moveCaretAfterText: false, visualSpan: visualSpan);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Should delete the entire line range encompasing the selection and position the 
        /// caret at the start of the range for undo / redo
        /// </summary>
        [Test]
        public void DeleteLineSelection_Character()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 1, 1));
            _commandUtil.DeleteLineSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("cat" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Should delete the entire line range encompasing the selection and position the 
        /// caret at the start of the range for undo / redo
        /// </summary>
        [Test]
        public void DeleteLineSelection_Line()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewLine(_textView.GetLineRange(0));
            _commandUtil.DeleteLineSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("cat" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When deleting a block it should delete from the start of the span until the end
        /// of the line for every span.  Caret should be positioned at the start of the edit
        /// but backed off a single space due to 'virtualedit='.  This will be properly
        /// handled by the moveCaretForVirtualEdit function.  Ensure it's called
        /// </summary>
        [Test]
        public void DeleteLineSelection_Block()
        {
            Create("cat", "dog", "fish");
            _globalSettings.VirtualEdit = String.Empty;
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlock(1, 1, 0, 2));
            _commandUtil.DeleteLineSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("c", _textView.GetLine(0).GetText());
            Assert.AreEqual("d", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Should delete the contents of the line excluding the break and preserve the 
        /// original line indent
        /// </summary>
        [Test]
        public void ChangeLineSelection_Character()
        {
            Create("  cat", "dog");
            _localSettings.AutoIndent = true;
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 2, 2));
            _commandUtil.ChangeLineSelection(UnnamedRegister, visualSpan, specialCaseBlock: false);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
        }

        /// <summary>
        /// Don't preserve the original indent if the 'autoindent' flag is not set
        /// </summary>
        [Test]
        public void ChangeLineSelection_Character_NoAutoIndent()
        {
            Create("  cat", "dog");
            _localSettings.AutoIndent = false;
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 2, 2));
            _commandUtil.ChangeLineSelection(UnnamedRegister, visualSpan, specialCaseBlock: false);
            Assert.AreEqual("  cat", UnnamedRegister.StringValue);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.IsFalse(_textView.GetCaretVirtualPoint().IsInVirtualSpace);
        }

        /// <summary>
        /// Delete everything except the line break and preserve the original indent
        /// </summary>
        [Test]
        public void ChangeLineSelection_Line()
        {
            Create("  cat", " dog", "bear", "fish");
            _localSettings.AutoIndent = true;
            var visualSpan = VisualSpan.NewLine(_textView.GetLineRange(1, 2));
            _commandUtil.ChangeLineSelection(UnnamedRegister, visualSpan, specialCaseBlock: false);
            Assert.AreEqual("  cat", _textView.GetLine(0).GetText());
            Assert.AreEqual("", _textView.GetLine(1).GetText());
            Assert.AreEqual("fish", _textView.GetLine(2).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
            Assert.AreEqual(1, _textView.GetCaretVirtualPoint().VirtualSpaces);
        }

        /// <summary>
        /// When not special casing block this should behave like the other forms of 
        /// ChangeLineSelection
        /// </summary>
        [Test]
        public void ChangeLineSelection_Block_NoSpecialCase()
        {
            Create("  cat", "  dog", "bear", "fish");
            _localSettings.AutoIndent = true;
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlock(2, 1, 0, 2));
            _commandUtil.ChangeLineSelection(UnnamedRegister, visualSpan, specialCaseBlock: false);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("bear", _textView.GetLine(1).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
            Assert.AreEqual(2, _textView.GetCaretVirtualPoint().VirtualSpaces);
        }

        /// <summary>
        /// When special casing block this turns into a simple delete till the end of the line
        /// </summary>
        [Test]
        public void ChangeLineSelection_Block_SpecialCase()
        {
            Create("  cat", "  dog", "bear", "fish");
            _localSettings.AutoIndent = true;
            var visualSpan = VisualSpan.NewBlock(_textView.GetBlock(2, 1, 0, 2));
            _commandUtil.ChangeLineSelection(UnnamedRegister, visualSpan, specialCaseBlock: true);
            Assert.AreEqual("  ", _textView.GetLine(0).GetText());
            Assert.AreEqual("  ", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Delete the text on the line and start insert mode.  Needs to pass a transaction onto
        /// insert mode to get the proper undo behavior
        /// </summary>
        [Test]
        public void ChangeLines_OneLine()
        {
            Create("cat", "dog");
            var result = _commandUtil.ChangeLines(1, UnnamedRegister);
            AssertInsertWithTransaction(result);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual(0, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we use a transaction here to ensure the undo behavior is correct
        /// </summary>
        [Test]
        public void ChangeTillEndOfLine_MiddleOfLine()
        {
            Create("cat");
            _globalSettings.VirtualEdit = string.Empty;
            _textView.MoveCaretTo(1);
            var result = _commandUtil.ChangeTillEndOfLine(1, UnnamedRegister);
            AssertInsertWithTransaction(result);
            Assert.AreEqual("c", _textView.GetLine(0).GetText());
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure we create a linked change for ChangeSelection
        /// </summary>
        [Test]
        public void ChangeSelection_Character()
        {
            Create("the dog chased the ball");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 1, 2));
            var result = _commandUtil.ChangeSelection(UnnamedRegister, visualSpan);
            AssertInsertWithTransaction(result);
            Assert.AreEqual("t dog chased the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual("he", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// Make sure the character deletion positions the caret at the start of the span and
        /// updates the register
        /// </summary>
        [Test]
        public void DeleteSelection_Character()
        {
            Create("the dog chased the ball");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 1, 2));
            _commandUtil.DeleteSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("t dog chased the ball", _textView.GetLine(0).GetText());
            Assert.AreEqual("he", UnnamedRegister.StringValue);
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
        }

        /// <summary>
        /// When a full line is selected make sure that it doesn't include the line break
        /// in the deletion
        /// </summary>
        [Test]
        public void DeleteSelection_Character_FullLine()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 0, 3));
            _commandUtil.DeleteSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("", _textView.GetLine(0).GetText());
            Assert.AreEqual("dog", _textView.GetLine(1).GetText());
        }

        /// <summary>
        /// When a full line is selected and the selection extents into the line break 
        /// then the deletion should include the entire line including the line break
        /// </summary>
        [Test]
        public void DeleteSelection_Character_FullLineFromLineBreak()
        {
            Create("cat", "dog");
            var visualSpan = VisualSpan.NewCharacter(_textView.GetLineSpan(0, 0, 4));
            _commandUtil.DeleteSelection(UnnamedRegister, visualSpan);
            Assert.AreEqual("dog", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Make sure caret starts at the begining of the line when there is no auto-indent
        /// </summary>
        [Test]
        public void InsertLineAbove_KeepCaretAtStartWithNoAutoIndent()
        {
            Create("foo");
            _globalSettings.UseEditorIndent = false;
            _commandUtil.InsertLineAbove(1);
            var point = _textView.Caret.Position.VirtualBufferPosition;
            Assert.IsFalse(point.IsInVirtualSpace);
            Assert.AreEqual(0, point.Position.Position);
        }

        /// <summary>
        /// Make sure the ending is placed correctly when done from the middle of the line
        /// </summary>
        [Test]
        public void InsertLineAbove_MiddleOfLine()
        {
            Create("foo", "bar");
            _globalSettings.UseEditorIndent = false;
            _textView.Caret.MoveTo(_textView.TextSnapshot.GetLineFromLineNumber(1).Start);
            _commandUtil.InsertLineAbove(1);
            var point = _textView.Caret.Position.BufferPosition;
            Assert.AreEqual(1, point.GetContainingLine().LineNumber);
            Assert.AreEqual(String.Empty, point.GetContainingLine().GetText());
        }

        /// <summary>
        /// Make sure we properly handle edits in the middle of our edit.  This happens 
        /// when the language service does a format for a new line
        /// </summary>
        [Test]
        public void InsertLineAbove_EditInTheMiddle()
        {
            Create("foo bar", "baz");

            bool didEdit = false;

            _textView.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _textView.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _globalSettings.UseEditorIndent = false;
            _commandUtil.InsertLineAbove(1);
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a ", line.GetText());
        }

        /// <summary>
        /// Maintain current indent when 'autoindent' is set but do so in virtual space
        /// </summary>
        [Test]
        public void InsertLineAbove_ShouldKeepIndentWhenAutoIndentSet()
        {
            Create("  cat", "dog");
            _globalSettings.UseEditorIndent = false;
            _localSettings.AutoIndent = true;
            _commandUtil.InsertLineAbove(1);
            Assert.AreEqual(2, _textView.Caret.Position.VirtualSpaces);
        }

        /// <summary>
        /// Insert from middle of line and enure it works out
        /// </summary>
        [Test]
        public void InsertLineBelow_InMiddleOfLine()
        {
            Create("foo", "bar", "baz");
            _commandUtil.InsertLineBelow(1);
            Assert.AreEqual(String.Empty, _textView.GetLine(1).GetText());
            Assert.AreEqual(_textView.GetLine(1).Start, _textView.GetCaretPoint());
        }

        /// <summary>
        /// Insert a new line at the end of the buffer and ensure it works.  Bit of a corner
        /// case since it won't have a line break
        /// </summary>
        [Test]
        public void InsertLineBelow_AtEndOfBuffer()
        {
            Create("foo", "bar");
            _textView.Caret.MoveTo(_textView.GetLine(1).End);
            _commandUtil.InsertLineBelow(1);
            Assert.AreEqual("", _textView.GetLine(2).GetText());
        }

        /// <summary>
        /// Deeply verify the contents of an insert below
        /// </summary>
        [Test]
        public void InsertLineBelow_Misc()
        {
            Create("foo bar", "baz");
            _commandUtil.InsertLineBelow(1);
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual(Environment.NewLine, line.GetLineBreakText());
            Assert.AreEqual(2, line.LineBreakLength);
            Assert.AreEqual("foo bar", line.GetText());
            Assert.AreEqual("foo bar" + Environment.NewLine, line.GetTextIncludingLineBreak());

            line = buffer.CurrentSnapshot.GetLineFromLineNumber(1);
            Assert.AreEqual(Environment.NewLine, line.GetLineBreakText());
            Assert.AreEqual(2, line.LineBreakLength);
            Assert.AreEqual(String.Empty, line.GetText());
            Assert.AreEqual(String.Empty + Environment.NewLine, line.GetTextIncludingLineBreak());

            line = buffer.CurrentSnapshot.GetLineFromLineNumber(2);
            Assert.AreEqual(String.Empty, line.GetLineBreakText());
            Assert.AreEqual(0, line.LineBreakLength);
            Assert.AreEqual("baz", line.GetText());
            Assert.AreEqual("baz", line.GetTextIncludingLineBreak());
        }

        /// <summary>
        /// Make sure that editor indent trumps 'autoindent'
        /// </summary>
        [Test]
        public void InsertLineBelow_PreferEditorIndent()
        {
            Create("cat", "dog");
            _globalSettings.UseEditorIndent = true;
            _smartIdentationService.Setup(x => x.GetDesiredIndentation(_textView, It.IsAny<ITextSnapshotLine>())).Returns(8);
            _commandUtil.InsertLineBelow(1);
            Assert.AreEqual(8, _textView.Caret.Position.VirtualSpaces);
        }

        /// <summary>
        /// Use Vim settings if the 'useeditorindent' setting is not present
        /// </summary>
        [Test]
        public void InsertLineBelow_RevertToVimIndentIfEditorIndentFails()
        {
            Create("  cat", "  dog");
            _globalSettings.UseEditorIndent = false;
            _localSettings.AutoIndent = true;
            _smartIdentationService.Setup(x => x.GetDesiredIndentation(_textView, It.IsAny<ITextSnapshotLine>())).Returns((int?)null);
            _commandUtil.InsertLineBelow(1);
            Assert.AreEqual(2, _textView.Caret.Position.VirtualSpaces);
        }

        /// <summary>
        /// Nested edits occur when the language service formats our new line.  Make
        /// sure we can handle it.
        /// </summary>
        [Test]
        public void InsertLineBelow_EditsInTheMiddle()
        {
            Create("foo bar", "baz");

            bool didEdit = false;

            _textView.TextBuffer.Changed += (sender, e) =>
            {
                if (didEdit)
                    return;

                using (var edit = _textView.TextBuffer.CreateEdit())
                {
                    edit.Insert(0, "a ");
                    edit.Apply();
                }

                didEdit = true;
            };

            _commandUtil.InsertLineBelow(1);
            var buffer = _textView.TextBuffer;
            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(0);
            Assert.AreEqual("a foo bar", line.GetText());
        }

        /// <summary>
        /// Maintain indent when using autoindent
        /// </summary>
        [Test]
        public void InsertLineBelow_KeepIndentWhenAutoIndentSet()
        {
            Create("  cat", "dog");
            _globalSettings.UseEditorIndent = false;
            _localSettings.AutoIndent = true;
            _commandUtil.InsertLineBelow(1);
            Assert.AreEqual("", _textView.GetLine(1).GetText());
            Assert.AreEqual(2, _textView.Caret.Position.VirtualBufferPosition.VirtualSpaces);
        }

        /// <summary>
        /// Make sure it beeps on a bad register name
        /// </summary>
        [Test]
        public void RecordMacroStart_BadRegisterName()
        {
            Create("");
            _recorder.SetupGet(x => x.IsRecording).Returns(false);
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.RecordMacroStart('!');
            _factory.Verify();
        }

        /// <summary>
        /// Upper case registers should cause an append to occur
        /// </summary>
        [Test]
        public void RecordMacroStart_AppendRegisters()
        {
            Create("");
            _recorder.SetupGet(x => x.IsRecording).Returns(false);
            _recorder.Setup(x => x.StartRecording(_registerMap.GetRegister('a'), true)).Verifiable();
            _commandUtil.RecordMacroStart('A');
            _factory.Verify();
        }

        /// <summary>
        /// Standard case where no append is needed
        /// </summary>
        [Test]
        public void RecordMacroStart_NormalRegister()
        {
            Create("");
            _recorder.SetupGet(x => x.IsRecording).Returns(false);
            _recorder.Setup(x => x.StartRecording(_registerMap.GetRegister('a'), false)).Verifiable();
            _commandUtil.RecordMacroStart('a');
            _factory.Verify();
        }

        /// <summary>
        /// Make sure it beeps on a bad register name
        /// </summary>
        [Test]
        public void RunMacro_BadRegisterName()
        {
            Create("");
            _operations.Setup(x => x.Beep()).Verifiable();
            _commandUtil.RunMacro('!', 1);
            _operations.Verify();
        }

        /// <summary>
        /// When jumping from a location not in the jump list and we're not in the middle 
        /// of a traversal the location should be added to the list
        /// </summary>
        [Test]
        public void JumpToOlderPosition_FromLocationNotInList()
        {
            Create("cat", "dog", "fish");
            _jumpList.Add(_textView.GetLine(1).Start);
            _commandUtil.JumpToOlderPosition(1);
            Assert.AreEqual(2, _jumpList.Jumps.Length);
            Assert.AreEqual(_textView.GetPoint(0), _jumpList.Jumps.Head.Position);
            Assert.AreEqual(1, _jumpList.CurrentIndex.Value);
        }

        /// <summary>
        /// When jumping from a location in the jump list and it's the start of the traversal
        /// then move the location back to the head of the class
        /// </summary>
        [Test]
        public void JumpToOlderPosition_FromLocationInList()
        {
            Create("cat", "dog", "fish");
            _jumpList.Add(_textView.GetLine(2).Start);
            _jumpList.Add(_textView.GetLine(1).Start);
            _jumpList.Add(_textView.GetLine(0).Start);
            _textView.MoveCaretToLine(1);
            _commandUtil.JumpToOlderPosition(1);
            Assert.AreEqual(3, _jumpList.Jumps.Length);
            CollectionAssert.AreEquivalent(
                _jumpList.Jumps.Select(x => x.Position),
                new[]
                {
                    _textView.GetLine(1).Start,
                    _textView.GetLine(0).Start,
                    _textView.GetLine(2).Start
                });
            Assert.AreEqual(1, _jumpList.CurrentIndex.Value);
        }

        /// <summary>
        /// When jumping from a location not in the jump list and we in the middle of a 
        /// traversal don't add the location to the list
        /// </summary>
        [Test]
        public void JumpToOlderPosition_FromLocationNotInListDuringTraversal()
        {
            Create("cat", "dog", "fish");
            _jumpList.Add(_textView.GetLine(1).Start);
            _jumpList.Add(_textView.GetLine(0).Start);
            Assert.IsTrue(_jumpList.MoveOlder(1));
            _textView.MoveCaretToLine(2);
            _commandUtil.JumpToOlderPosition(1);
            Assert.AreEqual(2, _jumpList.Jumps.Length);
            Assert.AreEqual(_textView.GetPoint(0), _jumpList.Jumps.Head.Position);
            Assert.AreEqual(1, _jumpList.CurrentIndex.Value);
        }

        /// <summary>
        /// Jump to the next position should not add the current position 
        /// </summary>
        [Test]
        public void JumpToNextPosition_FromMiddle()
        {
            Create("cat", "dog", "fish");
            _jumpList.Add(_textView.GetLine(2).Start);
            _jumpList.Add(_textView.GetLine(1).Start);
            _jumpList.Add(_textView.GetLine(0).Start);
            _jumpList.MoveOlder(1);
            _commandUtil.JumpToNewerPosition(1);
            Assert.AreEqual(3, _jumpList.Jumps.Length);
            CollectionAssert.AreEquivalent(
                _jumpList.Jumps.Select(x => x.Position),
                new[]
                {
                    _textView.GetLine(0).Start,
                    _textView.GetLine(1).Start,
                    _textView.GetLine(2).Start
                });
            Assert.AreEqual(0, _jumpList.CurrentIndex.Value);
        }

        /// <summary>
        /// Ensure that yank lines does a line wise yank of the 'count' lines
        /// from the caret
        /// </summary>
        [Test]
        public void YankLines_Normal()
        {
            Create("cat", "dog", "bear");
            _commandUtil.YankLines(2, UnnamedRegister);
            Assert.AreEqual("cat" + Environment.NewLine + "dog" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Ensure that yank lines operates against the visual buffer and will yank 
        /// the folded text
        /// </summary>
        [Test]
        public void YankLines_StartOfFold()
        {
            Create("cat", "dog", "bear", "fish", "pig");
            _foldManager.CreateFold(_textView.GetLineRange(1, 2));
            _textView.MoveCaretToLine(1);
            _commandUtil.YankLines(2, UnnamedRegister);
            Assert.AreEqual("dog" + Environment.NewLine + "bear" + Environment.NewLine + "fish" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        /// <summary>
        /// Ensure that yanking over a fold will count the fold as one line
        /// </summary>
        [Test]
        public void YankLines_OverFold()
        {
            Create("cat", "dog", "bear", "fish", "pig");
            _foldManager.CreateFold(_textView.GetLineRange(1, 2));
            _commandUtil.YankLines(3, UnnamedRegister);
            var text = EditorUtil.CreateLines("cat", "dog", "bear", "fish") + Environment.NewLine;
            Assert.AreEqual(text, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }
    }
}
