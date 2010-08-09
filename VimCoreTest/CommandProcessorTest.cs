using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Vim.Modes.Command;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UnitTest;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using System.IO;
using Microsoft.FSharp.Collections;
using Vim.Modes;
using Vim.UnitTest.Mock;
using Microsoft.FSharp.Core;
using Vim.Extensions;

namespace VimCore.Test
{
    [TestFixture, RequiresSTA]
    public class CommandProcessorTest
    {
        private IWpfTextView _view;
        private MockFactory _factory;
        private Mock<IVimBuffer> _bufferData;
        private CommandProcessor _processorRaw;
        private ICommandProcessor _processor;
        private IRegisterMap _map;
        private Mock<IEditorOperations> _editOpts;
        private Mock<IOperations> _operations;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IFileSystem> _fileSystem;
        private Mock<IVimHost> _vimHost;

        public void Create(params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 0));
            _map = new RegisterMap();
            _factory = new MockFactory(MockBehavior.Strict);
            _editOpts = _factory.Create<IEditorOperations>();
            _vimHost = _factory.Create<IVimHost>();
            _operations = _factory.Create<IOperations>();
            _operations.SetupGet(x => x.EditorOperations).Returns(_editOpts.Object);
            _statusUtil = _factory.Create<IStatusUtil>(); 
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _bufferData = MockObjectFactory.CreateVimBuffer(
                _view,
                "test",
                MockObjectFactory.CreateVim(_map, host:_vimHost.Object).Object);
            _processorRaw = new Vim.Modes.Command.CommandProcessor(_bufferData.Object, _operations.Object, _statusUtil.Object, _fileSystem.Object);
            _processor = _processorRaw;
        }

        private void RunCommand(string input)
        {
            _processor.RunCommand(Microsoft.FSharp.Collections.ListModule.OfSeq(input));
        }

        private void TestNoRemap(string input, string lhs, string rhs, params KeyRemapMode[] modes)
        {
            TestMapCore(input, lhs, rhs, false, modes);
        }

        private void TestRemap(string input, string lhs, string rhs, params KeyRemapMode[] modes)
        {
            TestMapCore(input, lhs, rhs, true, modes);
        }

        private void TestMapCore(string input, string lhs, string rhs, bool allowRemap, params KeyRemapMode[] modes)
        {
            _operations.Setup(x => x.RemapKeys(lhs, rhs, modes, allowRemap)).Verifiable();
            RunCommand(input);
            _operations.Verify();
        }

        private void TestMapClear(string input, params KeyRemapMode[] modes)
        {
            _operations.Setup(x => x.ClearKeyMapModes(modes)).Verifiable();
            RunCommand(input);
            _operations.Verify();
        }

        private void TestUnmap(string input, string lhs, params KeyRemapMode[] modes)
        {
            _operations.Setup(x => x.UnmapKeys(lhs, modes)).Verifiable();
            RunCommand(input);
            _operations.Verify();
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
            _statusUtil.Setup(x => x.OnError(It.IsAny<string>())).Verifiable();
            RunCommand("400");
            _factory.Verify();
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
                .Setup(x => x.ShiftSpanLeft(1, _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak))
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
                .Setup(x => x.ShiftSpanLeft(1, span))
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
                .Setup(x => x.ShiftSpanLeft(1, span))
                .Verifiable();
            RunCommand("< 2");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight1()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftSpanRight(1, _view.TextSnapshot.GetLineFromLineNumber(0).ExtentIncludingLineBreak))
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
                .Setup(x => x.ShiftSpanRight(1, span))
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
                .Setup(x => x.ShiftSpanRight(1, span))
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
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_NotSupported_SubstituteConfirm)).Verifiable();
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            RunCommand("%s/foo/bar/c");
            _factory.Verify();
        }

        [Test,Ignore]
        public void Substitute15()
        {
            Create("foo bar baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "", span, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo//");
            _operations.Verify();
        }

        [Test]
        public void Substitute16()
        {
            Create("foo bar baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "b", span, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/b/");
            _operations.Verify();
        }

        [Test]
        public void Substitute17()
        {
            Create("foo bar baz");
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(tss, 0, tss.Length);
            _operations
                .Setup(x => x.Substitute("foo", "", span, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/");
            _operations.Verify();
        }

        [Test]
        public void Redo1()
        {
            Create("foo bar");
            _operations.Setup(x => x.Redo(1)).Verifiable();
            RunCommand("red");
            _operations.Verify();
        }

        [Test]
        public void Redo2()
        {
            Create("foo bar");
            _operations.Setup(x => x.Redo(1)).Verifiable();
            RunCommand("redo");
            _operations.Verify();
        }

        [Test]
        public void Redo3()
        {
            Create("foo");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CannotRun("real"))).Verifiable();
            RunCommand("real");
            _factory.Verify();
        }

        [Test]
        public void Undo1()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(1));
            RunCommand("u");
            _operations.Verify();
        }

        [Test]
        public void Undo2()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(1));
            RunCommand("undo");
            _operations.Verify();
        }

        [Test]
        public void Undo3()
        {
            Create("foo");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CannotRun("unreal"))).Verifiable();
            RunCommand("unreal");
            _factory.Verify();
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
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CannotRun("marksaoeu"))).Verifiable();
            RunCommand("marksaoeu");
            _factory.Verify();
        }

        [Test]
        public void Edit1()
        {
            Create("foo");
            _operations.Setup(x => x.ShowOpenFileDialog()).Verifiable();
            RunCommand("e");
            _operations.Verify();
        }

        [Test]
        public void Edit2()
        {
            Create("foo");
            _operations.Setup(x => x.ShowOpenFileDialog()).Verifiable();
            RunCommand("edi");
            _operations.Verify();
        }

        [Test]
        public void Edit3()
        {
            Create("bar");
            _operations.Setup(x => x.EditFile("foo.cs")).Verifiable();
            RunCommand("ed foo.cs");
            _operations.Verify();
        }

        [Test, Description("Make sure the starting e is not picked up as an :edit command")]
        public void Edit4()
        {
            Create("");
            _statusUtil.Setup(x => x.OnError(It.IsAny<string>())).Verifiable();
            RunCommand("endfunc");
            _statusUtil.Verify();
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
            _fileSystem.Setup(x => x.ReadAllLines(It.IsAny<string>())).Returns(FSharpOption<string[]>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CouldNotOpenFile(String.Empty))).Verifiable();
            RunCommand("source");
            _factory.Verify();
        }

        [Test]
        public void Source2()
        {
            Create("bar");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_NotSupported_SourceNormal)).Verifiable();
            RunCommand("source! boo");
            _factory.Verify();
        }

        [Test]
        public void Source3()
        {
            var text = new string[] { "set noignorecase" };
            _fileSystem.Setup(x => x.ReadAllLines("blah.txt")).Returns(FSharpOption.Create(text)).Verifiable();
            _operations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            RunCommand("source blah.txt");
            _operations.Verify();
            _fileSystem.Verify();
        }

        [Test]
        public void Source4()
        {
            var text = new string[] { "set noignorecase", "set nofoo" };
            _fileSystem.Setup(x => x.ReadAllLines("blah.txt")).Returns(FSharpOption.Create(text)).Verifiable();
            _operations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            _operations.Setup(x => x.ResetSetting("foo")).Verifiable();
            RunCommand("source blah.txt"); 
            _operations.Verify();
        }

        [Test, Description("RunCommand should strip off the : prefix")]
        public void RunCommand1()
        {
            var list = ListModule.OfSeq(":set nofoo");
            _operations.Setup(x => x.ResetSetting("foo")).Verifiable();
            _processor.RunCommand(list);
            _operations.Verify();
        }

        [Test]
        public void RunCommand2()
        {
            var command = "\"foo bar";
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CannotRun(command))).Verifiable();
            _processor.RunCommand(ListModule.OfSeq(command));
            _factory.Verify();
        }

        [Test]
        public void RunCommand3()
        {
            var command = " \"foo bar";
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_CannotRun(command))).Verifiable();
            _processor.RunCommand(ListModule.OfSeq(command));
            _factory.Verify();
        }

        [Test]
        public void Remap_noremap()
        {
            Create("");
            var modes = new KeyRemapMode[] { KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending };
            TestNoRemap("noremap l h", "l", "h", modes);
            TestNoRemap("nore l h", "l", "h", modes);
            TestNoRemap("no l h", "l", "h", modes);
        }

        [Test]
        public void Remap_noremap2()
        {
            Create("");
            var modes = new KeyRemapMode[] { KeyRemapMode.Insert, KeyRemapMode.Command };
            TestNoRemap("noremap! l h", "l", "h", modes);
            TestNoRemap("nore! l h", "l", "h", modes);
            TestNoRemap("no! l h", "l", "h", modes);
        }

        [Test]
        public void Remap_nnoremap()
        {
            Create("");
            TestNoRemap("nnoremap l h", "l", "h", KeyRemapMode.Normal);
            TestNoRemap("nnor l h", "l", "h", KeyRemapMode.Normal);
            TestNoRemap("nn l h", "l", "h", KeyRemapMode.Normal);
        }

        [Test]
        public void Remap_vnoremap()
        {
            Create("");
            TestNoRemap("vnoremap a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestNoRemap("vnor a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestNoRemap("vn a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
        }

        [Test]
        public void Remap_xnoremap()
        {
            Create("");
            TestNoRemap("xnoremap b c", "b", "c", KeyRemapMode.Visual);
        }

        [Test]
        public void Remap_snoremap()
        {
            Create("");
            TestNoRemap("snoremap a b", "a", "b", KeyRemapMode.Select);
        }

        [Test]
        public void Remap_onoremap()
        {
            Create("");
            TestNoRemap("onoremap a b", "a", "b", KeyRemapMode.OperatorPending);
        }

        [Test]
        public void Remap_inoremap()
        {
            Create("");
            TestNoRemap("inoremap a b", "a", "b", KeyRemapMode.Insert);
        }

        [Test]
        public void Remap_map1()
        {
            Create("");
            TestRemap("map a bc", "a", "bc", KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Select, KeyRemapMode.OperatorPending);
        }

        [Test]
        public void Remap_nmap1()
        {
            Create("");
            TestRemap("nmap a b", "a", "b", KeyRemapMode.Normal);
        }

        [Test]
        public void Remap_many1()
        {
            Create("");
            TestRemap("vmap a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestRemap("vm a b", "a", "b", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestRemap("xmap a b", "a", "b", KeyRemapMode.Visual);
            TestRemap("xm a b", "a", "b", KeyRemapMode.Visual);
            TestRemap("smap a b", "a", "b", KeyRemapMode.Select);
            TestRemap("omap a b", "a", "b", KeyRemapMode.OperatorPending);
            TestRemap("om a b", "a", "b", KeyRemapMode.OperatorPending);
            TestRemap("imap a b", "a", "b", KeyRemapMode.Insert);
            TestRemap("im a b", "a", "b", KeyRemapMode.Insert);
            TestRemap("cmap a b", "a", "b", KeyRemapMode.Command);
            TestRemap("cm a b", "a", "b", KeyRemapMode.Command);
            TestRemap("lmap a b", "a", "b", KeyRemapMode.Language);
            TestRemap("lm a b", "a", "b", KeyRemapMode.Language);
            TestRemap("map! a b", "a", "b", KeyRemapMode.Insert, KeyRemapMode.Command);
        }

        [Test]
        public void MapClear_Many1()
        {
            Create("");
            TestMapClear("mapc", KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.Command, KeyRemapMode.OperatorPending);
            TestMapClear("nmapc", KeyRemapMode.Normal);
            TestMapClear("vmapc", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestMapClear("xmapc", KeyRemapMode.Visual);
            TestMapClear("smapc", KeyRemapMode.Select);
            TestMapClear("omapc", KeyRemapMode.OperatorPending);
            TestMapClear("mapc!", KeyRemapMode.Insert, KeyRemapMode.Command);
            TestMapClear("imapc", KeyRemapMode.Insert);
            TestMapClear("cmapc", KeyRemapMode.Command);
        }

        [Test]
        public void Unmap_Many1()
        {
            Create("");
            TestUnmap("vunmap a ", "a", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestUnmap("vunm a ", "a", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestUnmap("xunmap a", "a", KeyRemapMode.Visual);
            TestUnmap("xunm a ", "a",  KeyRemapMode.Visual);
            TestUnmap("sunmap a ", "a", KeyRemapMode.Select);
            TestUnmap("ounmap a ", "a", KeyRemapMode.OperatorPending);
            TestUnmap("ounm a ", "a", KeyRemapMode.OperatorPending);
            TestUnmap("iunmap a ", "a", KeyRemapMode.Insert);
            TestUnmap("iunm a", "a", KeyRemapMode.Insert);
            TestUnmap("cunmap a ", "a", KeyRemapMode.Command);
            TestUnmap("cunm a ", "a", KeyRemapMode.Command);
            TestUnmap("lunmap a ", "a", KeyRemapMode.Language);
            TestUnmap("lunm a ", "a", KeyRemapMode.Language);
            TestUnmap("unmap! a ", "a", KeyRemapMode.Insert, KeyRemapMode.Command);
        }

        [Test]
        public void Write1()
        {
            Create("");
            _operations.Setup(x => x.Save()).Verifiable();
            RunCommand("w");
            _operations.Verify();
        }

        [Test]
        public void Write2()
        {
            Create("");
            _operations.Setup(x => x.Save()).Verifiable();
            RunCommand("write");
            _operations.Verify();
        }

        [Test]
        public void Write3()
        {
            Create("");
            _operations.Setup(x => x.SaveAs("foo")).Verifiable();
            RunCommand("write foo");
            _operations.Verify();
        }

        [Test]
        public void Write4()
        {
            Create("");
            _operations.Setup(x => x.SaveAs("foo")).Verifiable();
            RunCommand("w foo");
            _operations.Verify();
        }

        [Test]
        public void WriteAll1()
        {
            Create("");
            _operations.Setup(x => x.SaveAll()).Verifiable();
            RunCommand("wa");
            _operations.Verify();
        }

        [Test]
        public void WriteAll2()
        {
            Create("");
            _operations.Setup(x => x.SaveAll()).Verifiable();
            RunCommand("wall");
            _operations.Verify();
        }

        [Test]
        public void Quit1()
        {
            Create("");
            _operations.Setup(x => x.Close(true)).Verifiable();
            RunCommand("quit");
            _operations.Verify();
        }

        [Test]
        public void Quit2()
        {
            Create("");
            _operations.Setup(x => x.Close(true)).Verifiable();
            RunCommand("q");
            _operations.Verify();
        }

        [Test]
        public void Quit3()
        {
            Create("");
            _operations.Setup(x => x.Close(false)).Verifiable();
            RunCommand("q!");
            _operations.Verify();
        }

        [Test]
        public void QuitAll1()
        {
            Create("");
            _operations.Setup(x => x.CloseAll(true)).Verifiable();
            RunCommand("qall");
            _operations.Verify();
        }

        [Test]
        public void QuitAll2()
        {
            Create("");
            _operations.Setup(x => x.CloseAll(false)).Verifiable();
            RunCommand("qall!");
            _operations.Verify();
        }

        [Test]
        public void QuitAll3()
        {
            Create("");
            _operations.Setup(x => x.CloseAll(true)).Verifiable();
            RunCommand("qa");
            _operations.Verify();
        }

        [Test]
        public void TabNext1()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(1)).Verifiable();
            RunCommand("tabnext");
            _operations.Verify();
        }

        [Test]
        public void TabNext2()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(1)).Verifiable();
            RunCommand("tabn");
            _operations.Verify();
        }

        [Test]
        public void TabNext3()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(3)).Verifiable();
            RunCommand("tabn 3");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious1()
        {
            Create("");
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            RunCommand("tabprevious");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious2()
        {
            Create("");
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            RunCommand("tabp");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious3()
        {
            Create("");
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            RunCommand("tabN");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious4()
        {
            Create("");
            _operations.Setup(x => x.GoToPreviousTab(1)).Verifiable();
            RunCommand("tabNext");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious5()
        {
            Create("");
            _operations.Setup(x => x.GoToPreviousTab(42)).Verifiable();
            RunCommand("tabNext 42");
            _operations.Verify();
        }

        [Test]
        public void Split1()
        {
            Create("");
            _vimHost.Setup(x => x.SplitView(_view)).Verifiable();
            RunCommand("split");
            _factory.Verify();
        }

        [Test]
        public void Split2()
        {
            Create("");
            _vimHost.Setup(x => x.SplitView(_view)).Verifiable();
            RunCommand("sp");
            _factory.Verify();
        }

        [Test]
        public void Close1()
        {
            Create("");
            _vimHost.Setup(x => x.CloseView(_view, true)).Verifiable();
            RunCommand(":close");
            _factory.Verify();
        }
        
        [Test]
        public void Close2()
        {
            Create("");
            _vimHost.Setup(x => x.CloseView(_view, false)).Verifiable();
            RunCommand(":close!");
            _factory.Verify();
        }

        [Test]
        public void Close3()
        {
            Create("");
            _vimHost.Setup(x => x.CloseView(_view, false)).Verifiable();
            RunCommand(":clo!");
            _factory.Verify();
        }
        
    }
 }
