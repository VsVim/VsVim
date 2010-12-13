using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.Modes.Visual;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class VisualModeTest
    {
        private MockRepository _factory;
        private Mock<IVimHost> _host;
        private Mock<IWpfTextView> _view;
        private Mock<ITextCaret> _caret;
        private Mock<ITextSelection> _selection;
        private ITextBuffer _buffer;
        private Mock<IVimBuffer> _bufferData;
        private VisualMode _modeRaw;
        private IMode _mode;
        private IRegisterMap _map;
        private Mock<ICommonOperations> _operations;
        private Mock<ISelectionTracker> _tracker;
        private Mock<IFoldManager> _foldManager;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IEditorOperations> _editorOperations;

        public void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        public void Create2(
            ModeKind kind = ModeKind.VisualCharacter,
            params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _caret = _factory.Create<ITextCaret>();
            _view = _factory.Create<IWpfTextView>();
            _selection = _factory.Create<ITextSelection>();
            _selection.Setup(x => x.Clear());
            _selection.SetupSet(x => x.Mode = TextSelectionMode.Stream);
            _view.SetupGet(x => x.Caret).Returns(_caret.Object);
            _view.SetupGet(x => x.Selection).Returns(_selection.Object);
            _view.SetupGet(x => x.TextBuffer).Returns(_buffer);
            _view.SetupGet(x => x.TextSnapshot).Returns(() => _buffer.CurrentSnapshot);
            _view.SetupGet(x => x.IsClosed).Returns(false);
            _map = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice(_factory).Object);
            _tracker = _factory.Create<ISelectionTracker>();
            _tracker.Setup(x => x.Start());
            _tracker.Setup(x => x.ResetCaret());
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();
            _foldManager = _factory.Create<IFoldManager>();
            _editorOperations = _factory.Create<IEditorOperations>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.FoldManager).Returns(_foldManager.Object);
            _operations.SetupGet(x => x.UndoRedoOperations).Returns(_undoRedoOperations.Object);
            _operations.SetupGet(x => x.EditorOperations).Returns(_editorOperations.Object);
            _host = _factory.Create<IVimHost>(MockBehavior.Loose);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view.Object,
                "test",
                MockObjectFactory.CreateVim(_map, host: _host.Object).Object,
                factory: _factory);
            var capture = new MotionCapture(
                _host.Object,
                _view.Object,
                new TextViewMotionUtil(_view.Object, new Vim.LocalSettings(_bufferData.Object.Settings.GlobalSettings, _view.Object)),
                new MotionCaptureGlobalData());
            var runner = new CommandRunner(_view.Object, _map, (IMotionCapture)capture, (new Mock<IStatusUtil>()).Object);
            _modeRaw = new Vim.Modes.Visual.VisualMode(_bufferData.Object, _operations.Object, kind, runner, capture, _tracker.Object);
            _mode = _modeRaw;
            _mode.OnEnter(ModeArgument.None);
        }

        [Test, Description("Movement commands")]
        public void Commands1()
        {
            Create("foo");
            var list = new KeyInput[] {
                KeyInputUtil.CharToKeyInput('h'),
                KeyInputUtil.CharToKeyInput('j'),
                KeyInputUtil.CharToKeyInput('k'),
                KeyInputUtil.CharToKeyInput('l'),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Left),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Right),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Up),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Down),
                KeyInputUtil.VimKeyToKeyInput(VimKey.Back) };
            var commands = _mode.CommandNames.ToList();
            foreach (var item in list)
            {
                var name = KeyInputSet.NewOneKeyInput(item);
                Assert.Contains(name, commands);
            }
        }

        [Test]
        public void Process1()
        {
            Create("foo");
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test, Description("Escape should always escape even if we're processing an inner key sequence")]
        public void Process2()
        {
            Create("foo");
            _mode.Process('g');
            var res = _mode.Process(KeyInputUtil.EscapeKey);
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test]
        public void OnLeave1()
        {
            _tracker.Setup(x => x.Stop()).Verifiable();
            _mode.OnLeave();
            _tracker.Verify();
        }

        [Test]
        public void InExplicitMove1()
        {
            Create("foo");
            _modeRaw.BeginExplicitMove();
            Assert.IsTrue(_modeRaw.InExplicitMove);
        }

        [Test]
        public void InExplicitMove2()
        {
            Create("");
            Assert.IsFalse(_modeRaw.InExplicitMove);
            _modeRaw.BeginExplicitMove();
            _modeRaw.BeginExplicitMove();
            _modeRaw.EndExplicitMove();
            _modeRaw.EndExplicitMove();
            Assert.IsFalse(_modeRaw.InExplicitMove);
        }

        [Test, Description("Must handle arbitrary input to prevent changes but don't list it as a command")]
        public void PreventInput1()
        {
            Create(lines: "foo");
            var input = KeyInputUtil.CharToKeyInput('@');
            _operations.Setup(x => x.Beep()).Verifiable();
            Assert.IsFalse(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == input.Char));
            Assert.IsTrue(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.IsTrue(ret.IsProcessed);
            _operations.Verify();
        }

        [Test]
        [Description("Clear the selection when leaving Visual Mode")]
        public void ChangeModeToNormalShouldClearSelection()
        {
            Create(lines: "foo");
            _selection.Setup(x => x.Clear()).Verifiable();
            _mode.Process(KeyInputUtil.EscapeKey);
            _factory.Verify();
        }

        [Test]
        [Description("Selection should be visible for the command mode operation")]
        public void ChangeModeToCommandShouldNotClearSelection()
        {
            Create(lines: "foo");
            _selection.Setup(x => x.Clear()).Throws(new Exception());
            _mode.Process(':');
        }

        #region Operations

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            Assert.IsTrue(_mode.Process('y').IsSwitchPreviousMode);
            Assert.AreEqual("foo", _map.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test, Description("Yank should go back to normal mode")]
        public void Yank2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            var res = _mode.Process('y');
            Assert.IsTrue(res.IsSwitchPreviousMode);
            Assert.AreEqual("foo", _map.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _mode.Process("\"cy");
            Assert.AreEqual("foo", _map.GetRegister('c').StringValue);
        }

        [Test]
        [Description("Yank should reset the caret")]
        public void Yank4()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _tracker.Setup(x => x.ResetCaret()).Verifiable();
            _selection.MakeSelection(span);
            Assert.IsTrue(_mode.Process('y').IsSwitchPreviousMode);
            Assert.AreEqual("foo", _map.GetRegister(RegisterName.Unnamed).StringValue);
            _tracker.Verify();
        }


        [Test]
        public void Yank_Y_1()
        {
            Create("foo", "bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            Assert.IsTrue(_mode.Process('Y').IsSwitchPreviousMode);
            Assert.AreEqual(_buffer.GetLineRange(0).GetTextIncludingLineBreak(), _map.GetRegister(RegisterName.Unnamed).StringValue);
        }

        [Test]
        public void Yank_Y_2()
        {
            Create2(ModeKind.VisualLine, "foo", "bar");
            var span = _buffer.GetLineRange(0).ExtentIncludingLineBreak;
            _selection.MakeSelection(span);
            _mode.Process('y');
            Assert.AreEqual("foo" + Environment.NewLine, _map.GetRegister(RegisterName.Unnamed).StringValue);
            Assert.AreEqual(OperationKind.LineWise, _map.GetRegister(RegisterName.Unnamed).Value.OperationKind);
        }

        [Test]
        public void Yank_Y_3()
        {
            Create("foo", "bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            Assert.IsTrue(_mode.Process('Y').IsSwitchPreviousMode);
            Assert.AreEqual(_buffer.GetLineRange(0).GetTextIncludingLineBreak(), _map.GetRegister(RegisterName.Unnamed).StringValue);
            Assert.AreEqual(OperationKind.LineWise, _map.GetRegister(RegisterName.Unnamed).Value.OperationKind);
        }

        [Test]
        public void DeleteSelection1()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Start.GetSpan(2);
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("d");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Start.GetSpan(2);
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('c'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("\"cd");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection3()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Start.GetSpan(2);
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process("x");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection4()
        {
            Create("foo", "bar");
            var span = _buffer.GetLine(0).Start.GetSpan(2);
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            _mode.Process(VimKey.Delete);
            _operations.Verify();
        }

        [Test]
        public void Join1()
        {
            Create("a", "b", "c", "d", "e");
            var range = _buffer.GetLineRange(0, 2);
            _selection.MakeSelection(range.Extent);
            _operations
                .Setup(x => x.Join(range, JoinKind.RemoveEmptySpaces))
                .Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join2()
        {
            Create("a", "b", "c", "d", "e");
            var range = _buffer.GetLineRange(0, 3);
            _selection.MakeSelection(range.Extent);
            _operations
                .Setup(x => x.Join(range, JoinKind.RemoveEmptySpaces))
                .Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join3()
        {
            Create("a", "b", "c", "d", "e");
            var range = _buffer.GetLineRange(0, 3);
            _selection.MakeSelection(range.Extent);
            _operations
                .Setup(x => x.Join(range, JoinKind.KeepEmptySpaces))
                .Verifiable();
            _mode.Process("gJ");
            _operations.Verify();
        }

        [Test]
        public void Change1()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process('c');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _factory.Verify();
        }

        [Test]
        public void Change2()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister('b'), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process("\"bc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _factory.Verify();
        }

        [Test]
        public void Change3()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteSpan(span))
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process('s');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change4()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteLinesInSpan(span))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.LineWise))
                .Verifiable();
            var res = _mode.Process('S');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _factory.Verify();
        }

        [Test]
        public void Change5()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.DeleteLinesInSpan(span))
                .Returns(span)
                .Verifiable();
            _operations
                .Setup(x => x.UpdateRegisterForSpan(_map.GetRegister(RegisterName.Unnamed), RegisterOperation.Delete, span, OperationKind.CharacterWise))
                .Verifiable();
            var res = _mode.Process('C');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
            _factory.Verify();
        }

        [Test]
        public void ChangeCase1()
        {
            Create("foo bar", "baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ChangeLetterCase(span))
                .Verifiable();
            _selection.MakeSelection(span);
            _mode.Process('~');
            _selection.Verify();
            _operations.Verify();
        }

        [Test]
        public void ChangeCase2()
        {
            Create("foo", "bar", "baz");
            _selection.MakeSelection(
                _buffer.GetLineRange(0).Extent,
                _buffer.GetLineRange(1).Extent);
            _operations
                .Setup(x => x.ChangeLetterCaseBlock(It.IsAny<NormalizedSnapshotSpanCollection>()))
                .Verifiable();
            _mode.Process('~');
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftLineRangeLeft(1, SnapshotLineRangeUtil.CreateForSpan(span)))
                .Verifiable();
            _selection.MakeSelection(span);
            _mode.Process('<');
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftLeft2()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftLineRangeLeft(2, SnapshotLineRangeUtil.CreateForSpan(span)))
                .Verifiable();
            _selection.MakeSelection(span);
            _mode.Process("2<");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftLeft3()
        {
            Create("foo", "bar", "baz");
            _selection.MakeSelection(
                _buffer.GetLineRange(0).Extent,
                _buffer.GetLineRange(1).Extent);
            _operations
                .Setup(x => x.ShiftBlockLeft(1, It.IsAny<NormalizedSnapshotSpanCollection>()))
                .Verifiable();
            _mode.Process("<");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftRight1()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftLineRangeRight(1, SnapshotLineRangeUtil.CreateForSpan(span)))
                .Verifiable();
            _selection.MakeSelection(span);
            _mode.Process('>');
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftRight2()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftLineRangeRight(2, SnapshotLineRangeUtil.CreateForSpan(span)))
                .Verifiable();
            _selection.MakeSelection(span);
            _mode.Process("2>");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftRight3()
        {
            Create("foo", "bar", "baz");
            _selection.MakeSelection(
                _buffer.GetLineRange(0).Extent,
                _buffer.GetLineRange(1).Extent);
            _operations
                .Setup(x => x.ShiftBlockRight(1, It.IsAny<NormalizedSnapshotSpanCollection>()))
                .Verifiable();
            _mode.Process(">");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void Put1()
        {
            Create("foo bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.PasteOver(span, _map.GetRegister(RegisterName.Unnamed)))
                .Verifiable();
            _mode.Process('p');
            _factory.Verify();
        }

        [Test]
        public void Put2()
        {
            Create("foo bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _operations
                .Setup(x => x.PasteOver(span, _map.GetRegister('c')))
                .Verifiable();
            _mode.Process("\"cp");
            _factory.Verify();
        }

        [Test]
        public void Fold_zo()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _operations.Setup(x => x.OpenFold(span, 1)).Verifiable();
            _mode.Process("zo");
            _factory.Verify();
        }

        [Test]
        public void Fold_zc_1()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _operations.Setup(x => x.CloseFold(span, 1)).Verifiable();
            _mode.Process("zc");
            _factory.Verify();
        }

        [Test]
        public void Fold_zO()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _operations.Setup(x => x.OpenAllFolds(span)).Verifiable();
            _mode.Process("zO");
            _factory.Verify();
        }

        [Test]
        public void Fold_zC()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _operations.Setup(x => x.CloseAllFolds(span)).Verifiable();
            _mode.Process("zC");
            _factory.Verify();
        }

        [Test]
        public void Fold_zf()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _foldManager.Setup(x => x.CreateFold(span)).Verifiable();
            _mode.Process("zf");
            _factory.Verify();
        }

        [Test]
        public void Fold_zF_1()
        {
            Create("the", "quick", "brown", "fox");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _foldManager.Setup(x => x.CreateFold(_buffer.GetLineRange(0, 0).ExtentIncludingLineBreak)).Verifiable();
            _mode.Process("zF");
            _factory.Verify();
        }

        [Test]
        public void Fold_zF_2()
        {
            Create("the", "quick", "brown", "fox");
            var span = _buffer.GetSpan(0, 1);
            _selection.MakeSelection(span);
            _foldManager.Setup(x => x.CreateFold(_buffer.GetLineRange(0, 1).ExtentIncludingLineBreak)).Verifiable();
            _mode.Process("2zF");
            _factory.Verify();
        }

        [Test]
        public void Fold_zd()
        {
            Create("foo bar");
            _operations.Setup(x => x.DeleteOneFoldAtCursor()).Verifiable();
            _mode.Process("zd");
            _factory.Verify();
        }

        [Test]
        public void Fold_zD()
        {
            Create("foo bar");
            _operations.Setup(x => x.DeleteAllFoldsAtCursor()).Verifiable();
            _mode.Process("zD");
            _factory.Verify();
        }

        [Test]
        public void Fold_zE()
        {
            Create("foo bar");
            _foldManager.Setup(x => x.DeleteAllFolds()).Verifiable();
            _mode.Process("zE");
            _factory.Verify();
        }

        [Test]
        public void SwitchMode1()
        {
            Create("foo bar");
            var ret = _mode.Process(":");
            Assert.IsTrue(ret.IsSwitchModeWithArgument);
            Assert.AreEqual(ModeKind.Command, ret.AsSwitchModeWithArgument().Item1);
            Assert.AreEqual(ModeArgument.FromVisual, ret.AsSwitchModeWithArgument().Item2);
        }

        [Test]
        public void PageUp1()
        {
            Create("");
            _editorOperations.Setup(x => x.PageUp(false)).Verifiable();
            _tracker.Setup(x => x.UpdateSelection()).Verifiable();
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageUp>"));
            _factory.Verify();
        }

        [Test]
        public void PageDown1()
        {
            Create("");
            _editorOperations.Setup(x => x.PageDown(false)).Verifiable();
            _tracker.Setup(x => x.UpdateSelection()).Verifiable();
            _mode.Process(KeyNotationUtil.StringToKeyInput("<PageDown>"));
            _factory.Verify();
        }

        [Test]
        public void FormatSelection1()
        {
            Create("foo", "bar");
            var span = _buffer.GetLineRange(0).Extent;
            _selection.MakeSelection(span);
            _host.Setup(x => x.FormatLines(_view.Object, _buffer.GetLineRange(0, 0))).Verifiable();
            _mode.Process("=");
            _factory.Verify();
        }
        #endregion
    }
}
