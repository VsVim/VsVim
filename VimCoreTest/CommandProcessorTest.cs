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
using System.IO;
using Microsoft.FSharp.Collections;

namespace VimCoreTest
{
    [TestFixture, RequiresSTA]
    public class CommandProcessorTest
    {
        private IWpfTextView _view;
        private Mock<IVimBuffer> _bufferData;
        private CommandProcessor _processorRaw;
        private ICommandProcessor _processor;
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
                MockObjectFactory.CreateVim(_map, host: _host).Object,
                MockObjectFactory.CreateBlockCaret().Object,
                _editOpts.Object);
            _processorRaw = new Vim.Modes.Command.CommandProcessor(_bufferData.Object, _operations.Object);
            _processor = _processorRaw;
        }

        private void RunCommand(string input)
        {
            var list = input.Select(x => InputUtil.CharToKeyInput(x));
            _processor.RunCommand(Microsoft.FSharp.Collections.ListModule.OfSeq(list));
        }

        [Test]
        public void Jump1()
        {
            Create("foo", "bar", "baz");
            var tss = _view.TextSnapshot;
            var last = tss.LineCount - 1;
            _editOpts.Setup(x => x.MoveToEndOfDocument(false)).Verifiable();
            RunCommand("$");
            _editOpts.Verify();
        }

        [Test]
        public void Jump2()
        {
            Create("foo", "bar");
            _editOpts.Setup(x => x.GotoLine(1)).Verifiable();
            RunCommand("2");
            _editOpts.Verify();
        }

        [Test]
        public void Jump3()
        {
            Create("foo");
            RunCommand("400");
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
            RunCommand("y");
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
            RunCommand("1,2y");
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
            RunCommand("y c");
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
            RunCommand("y 2");
            _operations.Verify();
        }

        [Test]
        public void Put1()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.Put("hey", It.IsAny<ITextSnapshotLine>(), true)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            RunCommand("put");
            _operations.Verify();
        }

        [Test]
        public void Put2()
        {
            Create("foo", "bar");
            _operations.Setup(x => x.Put("hey", It.IsAny<ITextSnapshotLine>(), false)).Verifiable();
            _map.DefaultRegister.UpdateValue("hey");
            RunCommand("2put!");
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
            RunCommand("<");
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
            RunCommand("1,2<");
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
            RunCommand("< 2");
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
            RunCommand(">");
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
            RunCommand("1,2>");
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
            RunCommand("> 2");
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
            RunCommand("del");
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
            RunCommand("dele 2");
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
            RunCommand("2del");
            _operations.Verify();
        }

        [Test]
        public void Substitute1()
        {
            Create("foo bar");
            var span = _view.TextSnapshot.GetLineFromLineNumber(0).Extent;
            _operations
                .Setup(x => x.Substitute("f", "b", span, SubstituteFlags.None))
                .Verifiable();
            RunCommand("s/f/b");
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
            RunCommand("s/foo/bar");
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
            RunCommand("s/foo/bar/");
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
            RunCommand("s/foo/bar/g");
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
            RunCommand("s/foo/bar/i");
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
            RunCommand("s/foo/bar/gi");
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
            RunCommand("s/foo/bar/ig");
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
            RunCommand("s/foo/bar/n");
            _operations.Verify();
        }


        [Test]
        public void Substitute9()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/bar");
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
            RunCommand("%s/foo/bar/e");
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
            RunCommand("%s/foo/bar/I");
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
            RunCommand("%s/foo/bar/I");
            _operations.Verify();
            RunCommand("%s/foo/bar/&");
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
            RunCommand("%s/foo/bar/I");
            _operations.Verify();
            _operations
                .Setup(x => x.Substitute("foo", "bar", span, SubstituteFlags.OrdinalCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("%s/foo/bar/&g");
            _operations.Verify();
        }

        [Test]
        public void Substitute14()
        {
            Create("foo bar", "baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            RunCommand("%s/foo/bar/c");
            Assert.AreEqual(Resources.CommandMode_NotSupported_SubstituteConfirm, _host.Status);
        }

        [Test]
        public void Redo1()
        {
            Create("foo bar");
            RunCommand("red");
            Assert.AreEqual(1, _host.RedoCount);
        }

        [Test]
        public void Redo2()
        {
            Create("foo bar");
            RunCommand("redo");
            Assert.AreEqual(1, _host.RedoCount);
        }

        [Test]
        public void Redo3()
        {
            Create("foo");
            RunCommand("real");
            Assert.AreEqual(0, _host.RedoCount);
            Assert.AreEqual(Resources.CommandMode_CannotRun("real"), _host.Status);
        }

        [Test]
        public void Undo1()
        {
            Create("foo");
            RunCommand("u");
            Assert.AreEqual(1, _host.UndoCount);
        }

        [Test]
        public void Undo2()
        {
            Create("foo");
            RunCommand("undo");
            Assert.AreEqual(1, _host.UndoCount);
        }

        [Test]
        public void Undo3()
        {
            Create("foo");
            RunCommand("unreal");
            Assert.AreEqual(0, _host.UndoCount);
            Assert.AreEqual(Resources.CommandMode_CannotRun("unreal"), _host.Status);
        }

        [Test]
        public void Marks1()
        {
            Create("foo");
            _operations.Setup(x => x.PrintMarks(_bufferData.Object.MarkMap)).Verifiable();
            RunCommand("marks");
        }

        [Test]
        public void Marks2()
        {
            Create("foo");
            RunCommand("marksaoeu");
            Assert.AreEqual(Resources.CommandMode_CannotRun("marksaoeu"), _host.Status);
        }

        [Test]
        public void Edit1()
        {
            Create("foo");
            RunCommand("e");
            Assert.AreEqual(1, _host.ShowOpenFileDialogCount);
        }

        [Test]
        public void Edit2()
        {
            Create("foo");
            RunCommand("edi");
            Assert.AreEqual(1, _host.ShowOpenFileDialogCount);
        }

        [Test]
        public void Edit3()
        {
            Create("bar");
            _operations.Setup(x => x.EditFile("foo.cs")).Verifiable();
            RunCommand("ed foo.cs");
            _operations.Verify();
        }

        [Test]
        public void Set1()
        {
            Create("bar");
            _operations.Setup(x => x.PrintModifiedSettings()).Verifiable();
            RunCommand("se");
            _operations.Verify();
        }

        [Test]
        public void Set2()
        {
            Create("bar");
            _operations.Setup(x => x.PrintModifiedSettings()).Verifiable();
            RunCommand("set");
            _operations.Verify();
        }

        [Test]
        public void Set3()
        {
            Create("bar");
            _operations.Setup(x => x.PrintAllSettings()).Verifiable();
            RunCommand("se all");
            _operations.Verify();
        }

        [Test]
        public void Set4()
        {
            Create("bar");
            _operations.Setup(x => x.PrintAllSettings()).Verifiable();
            RunCommand("set all");
            _operations.Verify();
        }

        [Test]
        public void Set5()
        {
            Create("bar");
            _operations.Setup(x => x.PrintSetting("foo")).Verifiable();
            RunCommand("set foo?");
            _operations.Verify();
        }

        [Test]
        public void Set6()
        {
            Create("bar");
            _operations.Setup(x => x.OperateSetting("foo")).Verifiable();
            RunCommand("set foo");
            _operations.Verify();
        }

        [Test]
        public void Set7()
        {
            Create("bor");
            _operations.Setup(x => x.ResetSetting("foo")).Verifiable();
            RunCommand("set nofoo");
            _operations.Verify();
        }

        [Test]
        public void Set8()
        {
            Create("bar");
            _operations.Setup(x => x.InvertSetting("foo")).Verifiable();
            RunCommand("set foo!");
            _operations.Verify();
        }

        [Test]
        public void Set9()
        {
            Create("bar");
            _operations.Setup(x => x.InvertSetting("foo")).Verifiable();
            RunCommand("set invfoo");
            _operations.Verify();
        }

        [Test]
        public void Set10()
        {
            Create("bar");
            _operations.Setup(x => x.SetSettingValue("foo", "bar")).Verifiable();
            RunCommand("set foo=bar");
            _operations.Verify();
        }

        [Test]
        public void Set11()
        {
            Create("baa");
            _operations.Setup(x => x.SetSettingValue("foo", "true")).Verifiable();
            RunCommand("set foo=true");
            _operations.Verify();
        }

        [Test]
        public void Set12()
        {
            Create("baa");
            _operations.Setup(x => x.SetSettingValue("foo", "true")).Verifiable();
            RunCommand("set foo:true");
            _operations.Verify();
        }

        [Test]
        public void Source1()
        {
            Create("boo");
            RunCommand("source");
            Assert.AreEqual(Resources.CommandMode_CouldNotOpenFile(String.Empty), _host.Status);
        }

        [Test]
        public void Source2()
        {
            Create("bar");
            RunCommand("source! boo");
            Assert.AreEqual(Resources.CommandMode_NotSupported_SourceNormal, _host.Status);
        }

        [Test]
        public void Source3()
        {
            var name = Path.GetTempFileName();
            File.WriteAllText(name, "set noignorecase");

            _operations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            RunCommand("source " + name);
            _operations.Verify();
        }

        [Test]
        public void Source4()
        {
            var name = Path.GetTempFileName();
            File.WriteAllLines(name, new string[] { "set noignorecase", "set nofoo" });

            _operations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            _operations.Setup(x => x.ResetSetting("foo")).Verifiable();
            RunCommand("source " + name);
            _operations.Verify();
        }

        [Test, Description("RunCommand should strip off the : prefix")]
        public void RunCommand1()
        {
            var list = ListModule.OfSeq(":set nofoo".Select(x => InputUtil.CharToKeyInput(x)));
            _operations.Setup(x => x.ResetSetting("foo")).Verifiable();
            _processor.RunCommand(list);
            _operations.Verify();
        }
    }
}
