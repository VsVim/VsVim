using System;
using System.Collections.Generic;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.Interpreter;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    /// <summary>
    /// Tests from the old CommandProcessor implementation.  They have value but the majority of the functionality
    /// switched to Interpreter
    /// </summary>
    public sealed class CommandProcessorLegacyTest : VimTestBase
    {
        private ITextView _textView;
        private ITextBuffer _textBuffer;
        private MockRepository _factory;
        private IVimData _vimData;
        private Mock<IEditorOperations> _editOpts;
        private Mock<ICommonOperations> _operations;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IFileSystem> _fileSystem;
        private Mock<IFoldManager> _foldManager;
        private Mock<IVimHost> _vimHost;
        private Mock<IVim> _vim;
        private VimInterpreter _interpreter;

        public void Create(params string[] lines)
        {
            _textView = CreateTextView(lines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _textBuffer = _textView.TextBuffer;
            _factory = new MockRepository(MockBehavior.Strict);
            _editOpts = _factory.Create<IEditorOperations>();
            _vimHost = _factory.Create<IVimHost>();
            _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(false);
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.EditorOperations).Returns(_editOpts.Object);
            _statusUtil = _factory.Create<IStatusUtil>();
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _foldManager = _factory.Create<IFoldManager>(MockBehavior.Strict);
            _vimData = VimData;
            _vim = MockObjectFactory.CreateVim(RegisterMap, host: _vimHost.Object, vimData: _vimData, factory: _factory);
            var localSettings = new LocalSettings(Vim.GlobalSettings);
            var vimTextBuffer = MockObjectFactory.CreateVimTextBuffer(
                _textBuffer,
                vim: _vim.Object,
                localSettings: localSettings,
                factory: _factory);
            var vimBufferData = CreateVimBufferData(
                vimTextBuffer.Object,
                _textView,
                statusUtil: _statusUtil.Object);
            var vimBuffer = CreateVimBuffer(vimBufferData);
            _interpreter = new Interpreter.VimInterpreter(
                vimBuffer,
                _operations.Object,
                _foldManager.Object,
                _fileSystem.Object,
                _factory.Create<IBufferTrackingService>().Object);
        }

        private void RunCommand(string command)
        {
            if (command.StartsWith(":"))
            {
                command = command.Substring(1);
            }

            var lineCommand = VimUtil.ParseLineCommand(command);
            _interpreter.RunLineCommand(lineCommand);
        }

        /// <summary>
        /// Tests the given range text will produce the provided range
        /// </summary>
        private void TestRange(string rangeText, SnapshotLineRange range)
        {
            _operations.Setup(x => x.Join(range, JoinKind.RemoveEmptySpaces)).Verifiable();
            RunCommand(rangeText + "j");
            _operations.Verify();
        }

        /// <summary>
        /// Ensure the '$' / move to last line command is implemented properly
        /// </summary>
        [Fact]
        public void LastLine()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.MoveCaretToPoint(_textView.GetLastLine().Start, ViewFlags.Standard & ~ViewFlags.TextExpanded))
                .Verifiable();
            RunCommand("$");
            _operations.Verify();
        }

        /// <summary>
        /// Entering just a line number should jump to the corresponding Vim line number.  Note that Vim
        /// and ITextBuffer line numbers differ as Vim begins at 1
        /// </summary>
        [Fact]
        public void Jump_UseVimLineNumber()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPoint(_textView.GetLine(1).Start, ViewFlags.Standard & ~ViewFlags.TextExpanded)).Verifiable();
            RunCommand("2");
            _operations.Verify();
        }

        /// <summary>
        /// Even though Vim line numbers begin at 1, 0 is still a valid jump to the first line number 
        /// in Vim
        /// </summary>
        [Fact]
        public void Jump_FirstLineSpecial()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPoint(_textView.GetLine(0).Start, ViewFlags.Standard & ~ViewFlags.TextExpanded)).Verifiable();
            RunCommand("0");
            _operations.Verify();
        }

        /// <summary>
        /// When the line number exceeds the number of lines in the ITextBuffer it should just go to the
        /// last line number
        /// </summary>
        [Fact]
        public void Jump_LineNumberTooBig()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPoint(_textView.GetLine(2).Start, ViewFlags.Standard & ~ViewFlags.TextExpanded)).Verifiable();
            RunCommand("300");
            _operations.Verify();
        }

        /// <summary>
        /// Whichever line is targeted the point it jumps to should be the first non space / tab character on
        /// that line
        /// </summary>
        [Fact]
        public void Jump_Indent()
        {
            Create("cat", "  dog", "tree");
            _operations.Setup(x => x.MoveCaretToPoint(_textView.GetPointInLine(1, 2), ViewFlags.Standard & ~ViewFlags.TextExpanded)).Verifiable();
            RunCommand("2");
            _operations.Verify();
        }

        [Fact]
        public void Yank1()
        {
            Create("foo", "bar");
            RunCommand("y");
            Assert.Equal("foo" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.Equal(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        [Fact]
        public void Yank2()
        {
            Create("foo", "bar", "baz");
            RunCommand("1,2y");
            var text = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak.GetText();
            Assert.Equal(text, UnnamedRegister.StringValue);
        }

        [Fact]
        public void Yank3()
        {
            Create("foo", "bar");
            RunCommand("y c");
            Assert.Equal(_textView.GetLine(0).ExtentIncludingLineBreak.GetText(), RegisterMap.GetRegister('c').StringValue);
        }

        /// <summary>
        /// Ensure that an invalid line number still registers an error with commands line yank vs. chosing
        /// the last line in the ITextBuffer as it does for jump commands
        /// </summary>
        [Fact]
        public void Yank_InvalidLineNumber()
        {
            Create("hello", "world");
            _statusUtil.Setup(x => x.OnError(Resources.Range_Invalid)).Verifiable();
            RunCommand("300y");
            _statusUtil.Verify();
        }

        /// <summary>
        /// The count should be applied to the specified line number for yank
        /// </summary>
        [Fact]
        public void Yank_WithRangeAndCount()
        {
            Create("cat", "dog", "rabbit", "tree");
            RunCommand("2y 1");
            Assert.Equal("dog" + Environment.NewLine, UnnamedRegister.StringValue);
        }


        [Fact]
        public void Redo1()
        {
            Create("foo bar");
            _operations.Setup(x => x.Redo(1)).Verifiable();
            RunCommand("red");
            _operations.Verify();
        }

        [Fact]
        public void Redo2()
        {
            Create("foo bar");
            _operations.Setup(x => x.Redo(1)).Verifiable();
            RunCommand("redo");
            _operations.Verify();
        }

        [Fact]
        public void Undo1()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(1));
            RunCommand("u");
            _operations.Verify();
        }

        [Fact]
        public void Undo2()
        {
            Create("foo");
            _operations.Setup(x => x.Undo(1));
            RunCommand("undo");
            _operations.Verify();
        }

        [Fact]
        public void Edit_NoArgumentsShouldReload()
        {
            Create("foo");
            _vimHost.Setup(x => x.Reload(_textBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            _operations.Setup(x => x.MoveCaretToPoint(It.IsAny<SnapshotPoint>(), ViewFlags.Standard));
            RunCommand("e");
            _operations.Verify();
            RunCommand("edi");
            _factory.Verify();
        }

        [Fact]
        public void Edit_NoArgumentsButDirtyShouldError()
        {
            Create("");
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("e");
            _factory.Verify();
        }

        [Fact]
        public void Edit_FilePathButDirtyShouldError()
        {
            Create("foo");
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("e cat.txt");
            _factory.Verify();
        }

        /// <summary>
        /// Can't figure out how to make this fail so just beeping now
        /// </summary>
        [Fact]
        public void Edit_NoArgumentsReloadFailsShouldBeep()
        {
            Create("foo");
            _vimHost.Setup(x => x.Reload(_textBuffer)).Returns(false).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            _operations.Setup(x => x.Beep()).Verifiable();
            RunCommand("e");
            _factory.Verify();
        }

        [Fact]
        public void Edit_FilePathShouldLoadIntoExisting()
        {
            Create("");
            _vimHost.Setup(x => x.LoadFileIntoExistingWindow("cat.txt", _textView)).Returns(HostResult.Success).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            RunCommand("e cat.txt");
            _factory.Verify();
        }

        [Fact]
        public void WriteQuit_NoArguments()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textView.TextBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("wq");
            _factory.Verify();
        }

        [Fact]
        public void WriteQuit_WithBang()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textView.TextBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("wq!");
            _factory.Verify();
        }

        [Fact]
        public void WriteQuit_FileName()
        {
            Create("bar");
            _vimHost.Setup(x => x.SaveTextAs("bar", "foo.txt")).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("wq foo.txt");
            _factory.Verify();
        }

        [Fact]
        public void WriteQuit_Range()
        {
            Create("dog", "cat", "bear");
            _vimHost.Setup(x => x.SaveTextAs(It.IsAny<string>(), "foo.txt")).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("1,2wq foo.txt");
            _factory.Verify();
        }

        [Fact]
        public void Quit1()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("quit");
            _factory.Verify();
        }

        [Fact]
        public void Quit2()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("q");
            _factory.Verify();
        }

        [Fact]
        public void Quit3()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand("q!");
            _factory.Verify();
        }

        /// <summary>
        /// When provided the ! bang option the application should just rudely exit
        /// </summary>
        [Fact]
        public void QuitAll_WithBang()
        {
            Create("");
            _vimHost.Setup(x => x.Quit()).Verifiable();
            RunCommand("qall!");
            _vimHost.Verify();
        }

        /// <summary>
        /// If there are no dirty files then we should just be exiting and not raising any messages
        /// </summary>
        [Fact]
        public void QuitAll_WithNoDirty()
        {
            Create("");
            var buffer = _factory.Create<IVimBuffer>();
            buffer.SetupGet(x => x.TextBuffer).Returns(_factory.Create<ITextBuffer>().Object);
            var list = new List<IVimBuffer>() { buffer.Object };
            _vim.SetupGet(x => x.VimBuffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(false).Verifiable();
            _vimHost.Setup(x => x.Quit()).Verifiable();
            RunCommand("qall");
            _factory.Verify();
        }

        /// <summary>
        /// If there are dirty buffers and the ! option is missing then an error needs to be raised
        /// </summary>
        [Fact]
        public void QuitAll_WithDirty()
        {
            Create("");
            var buffer = _factory.Create<IVimBuffer>();
            buffer.SetupGet(x => x.TextBuffer).Returns(_factory.Create<ITextBuffer>().Object);
            var list = new List<IVimBuffer>() { buffer.Object };
            _vim.SetupGet(x => x.VimBuffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("qall");
            _factory.Verify();
        }

        [Fact]
        public void Split1()
        {
            Create("");
            _vimHost.Setup(x => x.SplitViewHorizontally(_textView)).Returns(HostResult.Success).Verifiable();
            RunCommand("split");
            _factory.Verify();
        }

        [Fact]
        public void Split2()
        {
            Create("");
            _vimHost.Setup(x => x.SplitViewHorizontally(_textView)).Returns(HostResult.Success).Verifiable();
            RunCommand("sp");
            _factory.Verify();
        }

        [Fact]
        public void Close1()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand(":close");
            _factory.Verify();
        }

        [Fact]
        public void Close2()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand(":close!");
            _factory.Verify();
        }

        [Fact]
        public void Close3()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView)).Verifiable();
            RunCommand(":clo!");
            _factory.Verify();
        }

        [Fact]
        public void Join_NoArguments()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("j");
            _factory.Verify();
        }

        [Fact]
        public void Join_WithBang()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.KeepEmptySpaces))
                .Verifiable();
            RunCommand("j!");
            _factory.Verify();
        }

        [Fact]
        public void Join_WithCount()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("j 3");
            _factory.Verify();
        }

        [Fact]
        public void Join_WithRangeAndCount()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(1, 3), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("2j 3");
            _factory.Verify();
        }

        /// <summary>
        /// Final count overrides the range and in case of 1 does nothing
        /// </summary>
        [Fact]
        public void Join_WithRangeAndCountOfOne()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(2, 2), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("3j 1");
            _factory.Verify();
        }

        [Fact]
        public void Range_CurrentLineWithIncrement()
        {
            Create("dog", "cat", "bear", "fish", "tree");
            TestRange(".,+2", _textView.GetLineRange(0, 2));
        }

    }
}
