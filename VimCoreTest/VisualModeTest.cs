using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.Modes.Visual;
using VimCore.Test.Mock;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class VisualModeTest
    {
        private MockFactory _factory;
        private Mock<IWpfTextView> _view;
        private Mock<ITextCaret> _caret;
        private Mock<ITextSelection> _selection;
        private ITextBuffer _buffer;
        private Mock<IVimBuffer> _bufferData;
        private VisualMode _modeRaw;
        private IMode _mode;
        private IRegisterMap _map;
        private Mock<IOperations> _operations;
        private Mock<ISelectionTracker> _tracker;
        private Mock<IFoldManager> _foldManager;

        public void Create(params string[] lines)
        {
            Create2(lines: lines);
        }

        public void Create2(
            ModeKind kind=ModeKind.VisualCharacter, 
            params string[] lines)
        {
            _buffer = EditorUtil.CreateBuffer(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _caret = _factory.Create<ITextCaret>();
            _view = _factory.Create<IWpfTextView>();
            _selection = _factory.Create<ITextSelection>();
            _view.SetupGet(x => x.Caret).Returns(_caret.Object);
            _view.SetupGet(x => x.Selection).Returns(_selection.Object);
            _view.SetupGet(x => x.TextBuffer).Returns(_buffer);
            _view.SetupGet(x => x.TextSnapshot).Returns(() => _buffer.CurrentSnapshot);
            _map = new RegisterMap();
            _tracker = _factory.Create<ISelectionTracker>();
            _tracker.Setup(x => x.Start());
            _foldManager = _factory.Create<IFoldManager>();
            _operations = _factory.Create<IOperations>();
            _operations.SetupGet(x => x.FoldManager).Returns(_foldManager.Object);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view.Object,
                "test",
                MockObjectFactory.CreateVim(_map).Object,
                factory:_factory);
            var capture = new MotionCapture(_view.Object, new MotionUtil(_view.Object, _bufferData.Object.Settings.GlobalSettings));
            var runner = new CommandRunner(_view.Object, _map, (IMotionCapture)capture, (new Mock<IStatusUtil>()).Object);
            _modeRaw = new Vim.Modes.Visual.VisualMode(_bufferData.Object, _operations.Object, kind, runner, capture, _tracker.Object);
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        public void SetupApplyAsSingleEdit()
        {
            _operations
                .Setup(x => x.ApplyAsSingleEdit(
                                It.IsAny<FSharpOption<string>>(),
                                It.IsAny<IEnumerable<SnapshotSpan>>(),
                                It.IsAny<FSharpFunc<SnapshotSpan, Unit>>()))
                .Callback<FSharpOption<string>,IEnumerable<SnapshotSpan>, FSharpFunc<SnapshotSpan,Unit>>((unused, spans, func) =>
                {
                    foreach (var span in spans)
                    {
                        func.Invoke(span);
                    }
                })
                .Verifiable();
        }

        public SnapshotSpan[] SetupBlockSelection()
        {
            SetupApplyAsSingleEdit();
            var spans = new SnapshotSpan[] { _buffer.GetSpan(0, 2), _buffer.GetSpan(3, 2) };
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(spans))
                .Verifiable();
            return spans;
        }

        [Test,Description("Movement commands")]
        public void Commands1()
        {
            Create("foo");
            var list = new KeyInput[] {
                InputUtil.CharToKeyInput('h'),
                InputUtil.CharToKeyInput('j'),
                InputUtil.CharToKeyInput('k'),
                InputUtil.CharToKeyInput('l'),
                InputUtil.VimKeyToKeyInput(VimKey.Left),
                InputUtil.VimKeyToKeyInput(VimKey.Right),
                InputUtil.VimKeyToKeyInput(VimKey.Up),
                InputUtil.VimKeyToKeyInput(VimKey.Down),
                InputUtil.VimKeyToKeyInput(VimKey.Back) };
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
            var res = _mode.Process(InputUtil.VimKeyToKeyInput(VimKey.Escape));
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test, Description("Escape should always escape even if we're processing an inner key sequence")]
        public void Process2()
        {
            Create("foo");
            _mode.Process('g');
            var res = _mode.Process(VimKey.Escape);
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

        [Test,Description("Must handle arbitrary input to prevent changes but don't list it as a command")]
        public void PreventInput1()
        {
            Create(lines:"foo");
            var input = InputUtil.CharToKeyInput(',');
            _operations.Setup(x => x.Beep()).Verifiable();
            Assert.IsFalse(_mode.CommandNames.Any(x => x.KeyInputs.First().Char == input.Char));
            Assert.IsTrue(_mode.CanProcess(input));
            var ret = _mode.Process(input);
            Assert.IsTrue(ret.IsProcessed);
            _operations.Verify();
        }

        #region Operations

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            Assert.IsTrue(_mode.Process('y').IsSwitchPreviousMode);
            _operations.Verify();
            _tracker.Verify();
        }

        [Test, Description("Yank should go back to normal mode")]
        public void Yank2()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.DefaultRegister)).Verifiable();
            var res = _mode.Process('y');
            Assert.IsTrue(res.IsSwitchPreviousMode);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            _tracker.SetupGet(x => x.SelectedText).Returns("foo").Verifiable();
            _operations.Setup(x => x.YankText("foo", MotionKind.Inclusive, OperationKind.CharacterWise, _map.GetRegister('c'))).Verifiable();
            _mode.Process("\"cy");
            _operations.Verify();
        }

        [Test]
        public void YankLines1()
        {
            Create("foo","bar");
            var tss = _buffer.CurrentSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _selection.SetupGet(x => x.Start).Returns(new VirtualSnapshotPoint(line.Start)).Verifiable();
            _selection.SetupGet(x => x.End).Returns(new VirtualSnapshotPoint(line.End)).Verifiable();
            _operations.Setup(x => x.Yank(line.ExtentIncludingLineBreak, MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            Assert.IsTrue(_mode.Process('Y').IsSwitchPreviousMode);
            _selection.Verify();
            _operations.Verify();
        }

        [Test, Description("Yank in visual line mode should always be a linewise yank")]
        public void YankLines2()
        {
            Create2(ModeKind.VisualLine, null, "foo", "bar");
            var tss = _buffer.CurrentSnapshot;
            var line = tss.GetLineFromLineNumber(0);
            _tracker.Setup(x => x.SelectedText).Returns("foo" + Environment.NewLine).Verifiable();
            _operations.Setup(x => x.YankText("foo" + Environment.NewLine, MotionKind.Inclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            _mode.Process('y');
            _tracker.Verify();
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection1()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Verifiable();
            _mode.Process("d");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection2()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.GetRegister('c')))
                .Verifiable();
            _mode.Process("\"cd");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection3()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Verifiable();
            _mode.Process("x");
            _operations.Verify();
        }

        [Test]
        public void DeleteSelection4()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Verifiable();
            _mode.Process(VimKey.Delete);
            _operations.Verify();
        }

        [Test]
        public void Join1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.RemoveEmptySpaces)).Returns(true).Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.RemoveEmptySpaces)).Returns(true).Verifiable();
            _mode.Process('J');
            _operations.Verify();
        }

        [Test]
        public void Join3()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.JoinSelection(JoinKind.KeepEmptySpaces)).Returns(true).Verifiable();
            _mode.Process("gJ");
            _operations.Verify();
        }

        [Test]
        public void Change1()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Verifiable();
            var res = _mode.Process('c');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change2()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.GetRegister('b')))
                .Verifiable();
            var res = _mode.Process("\"bc");
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change3()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelection(_map.DefaultRegister))
                .Verifiable();
            var res = _mode.Process('s');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change4()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelectedLines(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('S');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void Change5()
        {
            Create("foo", "bar");
            _operations
                .Setup(x => x.DeleteSelectedLines(_map.DefaultRegister))
                .Returns((ITextSnapshot)null)
                .Verifiable();
            var res = _mode.Process('C');
            Assert.IsTrue(res.IsSwitchMode);
            Assert.AreEqual(ModeKind.Insert, res.AsSwitchMode().Item);
        }

        [Test]
        public void ChangeCase1()
        {
            Create("foo bar", "baz");
            var span = _buffer.GetSpan(0,3);
            _operations
                .Setup(x => x.ChangeLetterCase(span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
            _mode.Process('~');
            _selection.Verify();
            _operations.Verify();
        }

        [Test]
        public void ChangeCase2()
        {
            Create("foo bar baz");
            var spans = SetupBlockSelection();
            var count = 0;
            _operations
                .Setup(x => x.ChangeLetterCase(It.IsAny<SnapshotSpan>()))
                .Callback(() => {count++;})
                .Verifiable();
            _mode.Process('~');
            _selection.Verify();
            _operations.Verify();
            Assert.AreEqual(count, 2);
        }

        [Test]
        public void ShiftLeft1()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftSpanLeft(1, span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
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
                .Setup(x => x.ShiftSpanLeft(2, span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
            _mode.Process("2<");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftLeft3()
        {
            Create("foo bar baz");
            var spans = SetupBlockSelection();
            var count = 0;
            _operations
                .Setup(x => x.ShiftSpanLeft(1, It.IsAny<SnapshotSpan>()))
                .Callback(() => { count++; });
            _mode.Process("<");
            _operations.Verify();
            _selection.Verify();
            Assert.AreEqual(spans.Length, count);
        }


        [Test]
        public void ShiftRight1()
        {
            Create("foo bar baz");
            var span = _buffer.GetSpan(0, 3);
            _operations
                .Setup(x => x.ShiftSpanRight(1, span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
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
                .Setup(x => x.ShiftSpanRight(2, span))
                .Verifiable();
            _selection
                .SetupGet(x => x.SelectedSpans)
                .Returns(new NormalizedSnapshotSpanCollection(span))
                .Verifiable();
            _mode.Process("2>");
            _operations.Verify();
            _selection.Verify();
        }

        [Test]
        public void ShiftRight3()
        {
            Create("foo bar baz");
            var spans = SetupBlockSelection();
            var count = 0;
            _operations
                .Setup(x => x.ShiftSpanRight(1, It.IsAny<SnapshotSpan>()))
                .Callback(() => { count++; });
            _mode.Process(">");
            _operations.Verify();
            _selection.Verify();
            Assert.AreEqual(spans.Length, count);
        }

        [Test]
        public void Put1()
        {
            Create("foo bar");
            _map.DefaultRegister.UpdateValue("");
            _operations
                .Setup(x => x.PasteOverSelection("", _map.DefaultRegister))
                .Verifiable();
            _mode.Process('p');
            _factory.Verify();
        }

        [Test]
        public void Put2()
        {
            Create("foo bar");
            _map.GetRegister('c').UpdateValue("");
            _operations
                .Setup(x => x.PasteOverSelection("", _map.GetRegister('c')))
                .Verifiable();
            _mode.Process("\"cp");
            _factory.Verify();
        }

        [Test]
        public void Put3()
        {
            Create("foo bar");
            _map.DefaultRegister.UpdateValue("again");
            _operations
                .Setup(x => x.PasteOverSelection("again", _map.DefaultRegister))
                .Verifiable();
            _mode.Process('p');
            _factory.Verify();
        }

        [Test]
        public void Fold_zo()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _operations.Setup(x => x.OpenFold(span, 1)).Verifiable();
            _mode.Process("zo");
            _factory.Verify();
        }

        [Test]
        public void Fold_zc_1()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _operations.Setup(x => x.CloseFold(span, 1)).Verifiable();
            _mode.Process("zc");
            _factory.Verify();
        }

        [Test]
        public void Fold_zO()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _operations.Setup(x => x.OpenAllFolds(span)).Verifiable();
            _mode.Process("zO");
            _factory.Verify();
        }

        [Test]
        public void Fold_zC()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _operations.Setup(x => x.CloseAllFolds(span)).Verifiable();
            _mode.Process("zC");
            _factory.Verify();
        }

        [Test]
        public void Fold_zf()
        {
            Create("foo bar");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _foldManager.Setup(x => x.CreateFold(span)).Verifiable();
            _mode.Process("zf");
            _factory.Verify();
        }

        [Test]
        public void Fold_zF_1()
        {
            Create("the", "quick", "brown", "fox");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span);
            _foldManager.Setup(x => x.CreateFold(_buffer.GetLineSpanIncludingLineBreak(0,0))).Verifiable();
            _mode.Process("zF");
            _factory.Verify();
        }

        [Test]
        public void Fold_zF_2()
        {
            Create("the", "quick", "brown", "fox");
            var span = _buffer.GetSpan(0, 1);
            _operations.Setup(x => x.SelectedSpan).Returns(span).Verifiable();
            _foldManager.Setup(x => x.CreateFold(_buffer.GetLineSpanIncludingLineBreak(0, 1))).Verifiable();
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

        #endregion
    }
}
