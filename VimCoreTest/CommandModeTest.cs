using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Operations;
using Moq;

namespace VimCoreTest
{
    [TestFixture, RequiresSTA]
    public class CommandModeTest
    {
        private IWpfTextView _view;
        private Mock<IVimBuffer> _bufferData;
        private CommandMode _modeRaw;
        private IMode _mode;
        private FakeVimHost _host;
        private IRegisterMap _map;
        private Mock<IEditorOperations> _editOpts;
        private Mock<IOperations> _operations;

        public void Create(params string[] lines)
        {
            _view = Utils.EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = new RegisterMap();
            _host = new FakeVimHost();
            _editOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _operations = new Mock<IOperations>(MockBehavior.Strict);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view,
                "test",
                MockObjectFactory.CreateVim(_map, host:_host).Object,
                MockObjectFactory.CreateBlockCaret().Object,
                _editOpts.Object);
            _modeRaw = new Vim.Modes.Command.CommandMode(Tuple.Create<IVimBuffer, IOperations>(_bufferData.Object, _operations.Object));
            _mode = _modeRaw;
            _mode.OnEnter();
        }

        private string InputString()
        {
            var inputs = _modeRaw.Input;
            if (inputs.Any())
            {
                return _modeRaw.Input.Select(x => x.Char.ToString()).Aggregate((l, r) => l + r);
            }
            return string.Empty;
        }

        private void ProcessWithEnter(string input)
        {
            _mode.Process(input);
            _mode.Process(InputUtil.KeyToKeyInput(Key.Enter));
        }

        [Test, Description("Entering command mode should update the status")]
        public void StatusOnColon1()
        {
            Create(String.Empty);
            _mode.OnEnter();
            Assert.AreEqual(":", _host.Status);
        }

        [Test, Description("When leaving command mode we should not clear the status because it will remove error messages")]
        public void StatusOnLeave()
        {
            Create(String.Empty);
            _host.Status = "foo";
            _mode.OnLeave();
            Assert.AreEqual("foo", _host.Status);
        }

        [Test]
        public void StatusOnProcess()
        {
            Create("foo", "bar");
            _host.Status = "foo";
            _editOpts.Setup(x => x.GotoLine(It.IsAny<int>()));
            _mode.Process("1");
            _mode.Process(InputUtil.KeyToKeyInput(Key.Enter));
            Assert.AreEqual(String.Empty, _host.Status);
        }

        [Test, Description("Ensure multiple commands can be processed")]
        public void DoubleCommand1()
        {
            Create("foo", "bar", "baz");
            _editOpts.Setup(x => x.GotoLine(1)).Verifiable();
            ProcessWithEnter("2");
            _editOpts.Setup(x => x.GotoLine(2)).Verifiable();
            ProcessWithEnter("3");
            _editOpts.Verify();
        }

        [Test]
        public void Jump1()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var last = tss.LineCount - 1;
            _editOpts.Setup(x => x.MoveToEndOfDocument(false)).Verifiable();
            ProcessWithEnter("$");
            _editOpts.Verify();
        }

        [Test]
        public void Jump2()
        {
            Create("foo", "bar");
            _editOpts.Setup(x => x.GotoLine(1)).Verifiable();
            ProcessWithEnter("2");
            _editOpts.Verify();
        }

        [Test]
        public void Jump3()
        {
            Create("foo");
            ProcessWithEnter("400");
            Assert.IsTrue(!String.IsNullOrEmpty(_host.Status));
        }

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            var tss = _view.TextSnapshot;
            _operations.Setup(x => x.Yank(
                tss.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Verifiable();
            ProcessWithEnter("y");
            _operations.Verify();
        }

        [Test]
        public void Yank2()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations.Setup(x => x.Yank(
                span,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.DefaultRegister)).Verifiable();
            ProcessWithEnter("1,2y");
            _operations.Verify();
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            var line = _view.TextSnapshot.GetLineFromLineNumber(0);
            _operations.Setup(x => x.Yank(
                line.ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.GetRegister('c'))).Verifiable();
            ProcessWithEnter("y c");
            _operations.Verify();
        }

        [Test]
        public void Yank4()
        {
            Create("foo", "bar");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations.Setup(x => x.Yank(span, MotionKind._unique_Exclusive, OperationKind.LineWise, _map.DefaultRegister)).Verifiable();
            ProcessWithEnter("y 2");
            _operations.Verify();
        }

        [Test]
        public void Put1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.Put("hey", It.IsAny<ITextSnapshotLine>(), true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            ProcessWithEnter("put");
            _operations.Verify();
        }

        [Test]
        public void Put2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.Put("hey", It.IsAny<ITextSnapshotLine>(), false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            ProcessWithEnter("2put!");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            Create("     foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftLeft(_view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter("<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft2()
        {
            Create("     foo", "     bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations
                .Setup(x => x.ShiftLeft(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter("1,2<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft3()
        {
            Create("     foo", "     bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations
                .Setup(x => x.ShiftLeft(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter("< 2");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight1()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftRight(_view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter(">");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight2()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations
                .Setup(x => x.ShiftRight(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter("1,2>");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight3()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations
                .Setup(x => x.ShiftRight(span, 4))
                .Returns<ITextSnapshot>(null)
                .Verifiable();
            ProcessWithEnter("> 2");
            _operations.Verify();
        }

        [Test]
        public void Delete1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            ProcessWithEnter("del");
            _operations.Verify();
        }

        [Test]
        public void Delete2()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            _operations.Setup(x => x.DeleteSpan(
                span,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            ProcessWithEnter("dele 2");
            _operations.Verify();
        }

        [Test]
        public void Delete3()
        {
            Create("foo", "bar", "baz");
            _operations.Setup(x => x.DeleteSpan(
                _view.TextSnapshot.GetLineFromLineNumber(1).ExtentIncludingLineBreak,
                MotionKind._unique_Exclusive,
                OperationKind.LineWise,
                _map.DefaultRegister))
                .Returns(It.IsAny<ITextSnapshot>())
                .Verifiable();
            ProcessWithEnter("2del");
            _operations.Verify();
        }

        [Test]
        public void Input1()
        {
            Create("foo", "bar");
            _mode.Process("fo");
            Assert.AreEqual("fo", InputString());
        }

        [Test]
        public void Input2()
        {
            Create("foo", "bar");
            ProcessWithEnter("foo");
            Assert.AreEqual(String.Empty, InputString());
        }

        [Test]
        public void Input3()
        {
            Create("foo bar");
            _mode.Process("foo");
            _mode.Process(InputUtil.KeyToKeyInput(Key.Back));
            Assert.AreEqual("fo", InputString());
        }

        [Test]
        public void Input4()
        {
            Create("foo bar");
            _mode.Process("foo");
            _mode.Process(InputUtil.KeyToKeyInput(Key.Escape));
            Assert.AreEqual(string.Empty, InputString());
        }

        [Test, Description("Delete past the start of the command string")]
        public void Input5()
        {
            Create("foo bar");
            _mode.Process('c');
            _mode.Process(InputUtil.KeyToKeyInput(Key.Back));
            _mode.Process(InputUtil.KeyToKeyInput(Key.Back));
            Assert.AreEqual(String.Empty, InputString());
        }

        [Test, Description("Upper case letter")]
        public void Input6()
        {
            Create("foo bar");
            _mode.Process("BACK");
            Assert.AreEqual("BACK", InputString());
        }

        [Test]
        public void Input7()
        {
            Create("foo bar");
            _mode.Process("_bar");
            Assert.AreEqual("_bar", InputString());
        }

        [Test]
        public void Cursor1()
        {
            Create("foo bar");
            Assert.IsTrue(_view.Caret.IsHidden);
        }

        [Test]
        public void Cursor2()
        {
            Create("foo bar");
            _mode.OnLeave();
            Assert.IsFalse(_view.Caret.IsHidden);
        }

        [Test]
        public void Substitute1()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("f", "b", span, SubstituteFlags.None))
                .Verifiable();
            ProcessWithEnter("s/f/b");
            _operations.Verify();
        }


        [Test]
        public void Substitute2()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.None))
                .Verifiable();
            ProcessWithEnter("s/foo/bar");
            _operations.Verify();
        }

        [Test]
        public void Substitute3()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.None))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/");
            _operations.Verify();
        }

        [Test]
        public void Substitute4()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.ReplaceAll))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/g");
            _operations.Verify();
        }

        [Test]
        public void Substitute5()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.IgnoreCase))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/i");
            _operations.Verify();
        }

        [Test]
        public void Substitute6()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/gi");
            _operations.Verify();
        }

        [Test]
        public void Substitute7()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/ig");
            _operations.Verify();
        }


        [Test]
        public void Substitute8()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.ReportOnly))
                .Verifiable();
            ProcessWithEnter("s/foo/bar/n");
            _operations.Verify();
        }


        [Test]
        public void Substitute9()
        {
            Create("foo bar","baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.None))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar");
            _operations.Verify();
        }

        [Test]
        public void Substitute10()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.SuppressError))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar/e");
            _operations.Verify();
        }

        [Test]
        public void Substitute11()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.OrdinalCase))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar/I");
            _operations.Verify();
        }

        [Test, Description("Use last flags flag")]
        public void Substitute12()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.OrdinalCase))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar/I");
            _operations.Verify();
            ProcessWithEnter("%s/foo/bar/&");
            _operations.Verify();
        }

        [Test, Description("Use last flags flag plus new flags")]
        public void Substitute13()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.OrdinalCase))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar/I");
            _operations.Verify();
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.OrdinalCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            ProcessWithEnter("%s/foo/bar/&g");
            _operations.Verify();
        }

        [Test]
        public void Substitute14()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            ProcessWithEnter("%s/foo/bar/c");
            Assert.AreEqual(Resources.CommandMode_NotSupported_SubstituteConfirm, _host.Status);
        }

    }
}
