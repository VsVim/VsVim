using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.Command;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture, RequiresSTA]
    public class CommandProcessorTest
    {
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private MockRepository _factory;
        private Mock<IVimBuffer> _buffer;
        private CommandProcessor _processorRaw;
        private ICommandProcessor _processor;
        private IRegisterMap _map;
        private IVimData _vimData;
        private Mock<IEditorOperations> _editOpts;
        private Mock<ICommonOperations> _operations;
        private Mock<IOperations> _commandOperations;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IFileSystem> _fileSystem;
        private Mock<IFoldManager> _foldManager;
        private Mock<IVimHost> _vimHost;
        private Mock<IVim> _vim;

        public void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _textView.Caret.MoveTo(new SnapshotPoint(_textView.TextSnapshot, 0));
            _textBuffer = _textView.TextBuffer;
            _factory = new MockRepository(MockBehavior.Strict);
            _map = VimUtil.CreateRegisterMap(MockObjectFactory.CreateClipboardDevice(_factory).Object);
            _editOpts = _factory.Create<IEditorOperations>();
            _vimHost = _factory.Create<IVimHost>();
            _operations = _factory.Create<ICommonOperations>();
            _operations.SetupGet(x => x.EditorOperations).Returns(_editOpts.Object);
            _commandOperations = _factory.Create<IOperations>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _fileSystem = _factory.Create<IFileSystem>(MockBehavior.Strict);
            _foldManager = _factory.Create<IFoldManager>(MockBehavior.Strict);
            _vimData = new VimData();
            _vim = MockObjectFactory.CreateVim(_map, host: _vimHost.Object, vimData: _vimData, factory: _factory);
            _buffer = MockObjectFactory.CreateVimBuffer(
                _textView,
                "test",
                _vim.Object);
            _processorRaw = new CommandProcessor(
                _buffer.Object,
                _operations.Object,
                _commandOperations.Object,
                _statusUtil.Object,
                _fileSystem.Object,
                _foldManager.Object);
            _processor = _processorRaw;
        }

        private Register UnnamedRegister
        {
            get { return _map.GetRegister(RegisterName.Unnamed); }
        }

        private RunResult RunCommand(string input)
        {
            return _processor.RunCommand(Microsoft.FSharp.Collections.ListModule.OfSeq(input));
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
            _commandOperations.Setup(x => x.RemapKeys(lhs, rhs, modes, allowRemap)).Verifiable();
            RunCommand(input);
            _operations.Verify();
        }

        private void TestMapClear(string input, params KeyRemapMode[] modes)
        {
            _commandOperations.Setup(x => x.ClearKeyMapModes(modes)).Verifiable();
            RunCommand(input);
            _operations.Verify();
        }

        private void TestUnmap(string input, string lhs, params KeyRemapMode[] modes)
        {
            _commandOperations.Setup(x => x.UnmapKeys(lhs, modes)).Verifiable();
            RunCommand(input);
            _operations.Verify();
        }

        private void TestPrintMap(string input, params KeyRemapMode[] modes)
        {
            _commandOperations
                .Setup(x => x.PrintKeyMap(It.IsAny<FSharpList<KeyRemapMode>>()))
                .Callback<FSharpList<KeyRemapMode>>(
                    list =>
                    {
                        foreach (var mode in modes)
                        {
                            Assert.IsTrue(list.Contains(mode));
                        }
                    })
                .Verifiable();
            RunCommand(input);
            _factory.Verify();
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
        [Test]
        public void LastLine()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.MoveCaretToPointAndEnsureVisible(_textView.GetLastLine().Start))
                .Verifiable();
            RunCommand("$");
            _operations.Verify();
        }

        /// <summary>
        /// Entering just a line number should jump to the corresponding Vim line number.  Note that Vim
        /// and ITextBuffer line numbers differ as Vim begins at 1
        /// </summary>
        [Test]
        public void Jump_UseVimLineNumber()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(_textView.GetLine(1).Start)).Verifiable();
            RunCommand("2");
            _operations.Verify();
        }

        /// <summary>
        /// Even though Vim line numbers begin at 1, 0 is still a valid jump to the first line number 
        /// in Vim
        /// </summary>
        [Test]
        public void Jump_FirstLineSpecial()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(_textView.GetLine(0).Start)).Verifiable();
            RunCommand("0");
            _operations.Verify();
        }

        /// <summary>
        /// When the line number exceeds the number of lines in the ITextBuffer it should just go to the
        /// last line number
        /// </summary>
        [Test]
        public void Jump_LineNumberTooBig()
        {
            Create("cat", "dog", "tree");
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(_textView.GetLine(2).Start)).Verifiable();
            RunCommand("300");
            _operations.Verify();
        }

        /// <summary>
        /// Whichever line is targeted the point it jumps to should be the first non space / tab character on
        /// that line
        /// </summary>
        [Test]
        public void Jump_Indent()
        {
            Create("cat", "  dog", "tree");
            _operations.Setup(x => x.MoveCaretToPointAndEnsureVisible(_textView.GetPointInLine(1, 2))).Verifiable();
            RunCommand("2");
            _operations.Verify();
        }

        [Test]
        public void Yank1()
        {
            Create("foo", "bar");
            RunCommand("y");
            Assert.AreEqual("foo" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual(OperationKind.LineWise, UnnamedRegister.OperationKind);
        }

        [Test]
        public void Yank2()
        {
            Create("foo", "bar", "baz");
            RunCommand("1,2y");
            var text = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak.GetText();
            Assert.AreEqual(text, UnnamedRegister.StringValue);
        }

        [Test]
        public void Yank3()
        {
            Create("foo", "bar");
            RunCommand("y c");
            Assert.AreEqual(_textView.GetLine(0).ExtentIncludingLineBreak.GetText(), _map.GetRegister('c').StringValue);
        }

        [Test]
        public void Yank4()
        {
            Create("foo", "bar");
            RunCommand("y 2");
            var text = _textView.GetLineRange(0, 1).ExtentIncludingLineBreak.GetText();
            Assert.AreEqual(text, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Ensure that an invalid line number still registers an error with commands line yank vs. chosing
        /// the last line in the ITextBuffer as it does for jump commands
        /// </summary>
        [Test]
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
        [Test]
        public void Yank_WithRangeAndCount()
        {
            Create("cat", "dog", "rabbit", "tree");
            RunCommand("2y 1");
            Assert.AreEqual("dog" + Environment.NewLine, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Ensure the Put command is linewise even if the register is marked as characterwise
        /// </summary>
        [Test]
        public void Put_ShouldBeLinewise()
        {
            Create("foo", "bar");
            _commandOperations
                .Setup(x => x.PutLine(_map.GetRegister(RegisterName.Unnamed), It.IsAny<ITextSnapshotLine>(), false))
                .Callback<Register, ITextSnapshotLine, bool>((register, line, putBefore) => Assert.IsTrue(line.LineNumber == 0))
                .Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey", OperationKind.CharacterWise);
            RunCommand("put");
            _commandOperations.Verify();
        }

        /// <summary>
        /// Ensure that when the ! is present that the appropriate option is passed along
        /// </summary>
        [Test]
        public void Put_BangShouldPutTextBefore()
        {
            Create("foo", "bar");
            _commandOperations
                .Setup(x => x.PutLine(_map.GetRegister(RegisterName.Unnamed), It.IsAny<ITextSnapshotLine>(), true))
                .Callback<Register, ITextSnapshotLine, bool>((register, line, putBefore) => Assert.IsTrue(line.LineNumber == 0))
                .Verifiable();
            _map.GetRegister(RegisterName.Unnamed).UpdateValue("hey", OperationKind.CharacterWise);
            RunCommand("put!");
            _commandOperations.Verify();
        }

        [Test]
        public void ShiftLeft1()
        {
            Create("     foo", "bar", "baz");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.ShiftLineRangeLeft(range, 1))
                .Verifiable();
            RunCommand("<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft2()
        {
            Create("     foo", "     bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _operations
                .Setup(x => x.ShiftLineRangeLeft(range, 1))
                .Verifiable();
            RunCommand("1,2<");
            _operations.Verify();
        }

        [Test]
        public void ShiftLeft3()
        {
            Create("     foo", "     bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _operations
                .Setup(x => x.ShiftLineRangeLeft(range, 1))
                .Verifiable();
            RunCommand("< 2");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight1()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftLineRangeRight(_textView.GetLineRange(0, 0), 1))
                .Verifiable();
            RunCommand(">");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight2()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftLineRangeRight(_textView.GetLineRange(0, 1), 1))
                .Verifiable();
            RunCommand("1,2>");
            _operations.Verify();
        }

        [Test]
        public void ShiftRight3()
        {
            Create("foo", "bar", "baz");
            _operations
                .Setup(x => x.ShiftLineRangeRight(_textView.GetLineRange(0, 1), 1))
                .Verifiable();
            RunCommand("> 2");
            _operations.Verify();
        }

        /// <summary>
        /// No arguments means delete the current line
        /// </summary>
        [Test]
        public void Delete_CurrentLine()
        {
            Create("foo", "bar");
            RunCommand("del");
            Assert.AreEqual("foo" + Environment.NewLine, UnnamedRegister.StringValue);
            Assert.AreEqual("bar", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// When count is in back it's a range of lines
        /// </summary>
        [Test]
        public void Delete_SeveralLines()
        {
            Create("foo", "bar", "baz");
            RunCommand("dele 2");
            Assert.AreEqual("baz", _textView.GetLine(0).GetText());
            Assert.AreEqual("foo" + Environment.NewLine + "bar" + Environment.NewLine, UnnamedRegister.StringValue);
        }

        /// <summary>
        /// Delete only the specified line when count is in front
        /// </summary>
        [Test]
        public void Delete_SpecificLineNumber()
        {
            Create("foo", "bar", "baz");
            RunCommand("2del");
            Assert.AreEqual("foo", _textView.GetLine(0).GetText());
            Assert.AreEqual("baz", _textView.GetLine(1).GetText());
            Assert.AreEqual("bar" + Environment.NewLine, UnnamedRegister.StringValue);
        }

        [Test]
        public void Substitute1()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("f", "b", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("s/f/b");
            _operations.Verify();
        }


        [Test]
        public void Substitute2()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("s/foo/bar");
            _operations.Verify();
        }

        [Test]
        public void Substitute3()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("s/foo/bar/");
            _operations.Verify();
        }

        [Test]
        public void Substitute4()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("s/foo/bar/g");
            _operations.Verify();
        }

        [Test]
        public void Substitute5()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.IgnoreCase))
                .Verifiable();
            RunCommand("s/foo/bar/i");
            _operations.Verify();
        }

        [Test]
        public void Substitute6()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("s/foo/bar/gi");
            _operations.Verify();
        }

        [Test]
        public void Substitute7()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("s/foo/bar/ig");
            _operations.Verify();
        }


        [Test]
        public void Substitute8()
        {
            Create("foo bar");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.ReportOnly))
                .Verifiable();
            RunCommand("s/foo/bar/n");
            _operations.Verify();
        }


        [Test]
        public void Substitute9()
        {
            Create("foo bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/bar");
            _operations.Verify();
        }

        [Test]
        public void Substitute10()
        {
            Create("foo bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.SuppressError))
                .Verifiable();
            RunCommand("%s/foo/bar/e");
            _operations.Verify();
        }

        [Test]
        public void Substitute11()
        {
            Create("foo bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.OrdinalCase))
                .Verifiable();
            RunCommand("%s/foo/bar/I");
            _operations.Verify();
        }

        [Test, Description("Use last flags flag")]
        public void Substitute12()
        {
            Create("foo bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("", "", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.OrdinalCase))
                .Verifiable();
            RunCommand("%s/foo/bar/&");
            _operations.Verify();
        }

        [Test, Description("Use last flags flag plus new flags")]
        public void Substitute13()
        {
            Create("foo bar", "baz");
            var range = _textView.GetLineRange(0, 1);
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("", "", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("foo", "bar", range, SubstituteFlags.OrdinalCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("%s/foo/bar/&g");
            _operations.Verify();
        }

        [Test]
        public void Substitute14()
        {
            Create("foo bar", "baz");
            var result = RunCommand("%s/foo/bar/c");
            Assert.IsTrue(result.IsSubstituteConfirm);

            var confirm = result.AsSubstituteConfirm();
            Assert.AreEqual("foo", confirm.Item3.SearchPattern);
            Assert.AreEqual("bar", confirm.Item3.Substitute);
            _factory.Verify();
        }

        [Test]
        public void Substitute15()
        {
            Create("foo bar baz");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo//");
            _operations.Verify();
        }

        [Test]
        public void Substitute16()
        {
            Create("foo bar baz");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "b", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/b/");
            _operations.Verify();
        }

        [Test]
        public void Substitute17()
        {
            Create("foo bar baz");
            var range = _textView.GetLineRange(0);
            _operations
                .Setup(x => x.Substitute("foo", "", range, SubstituteFlags.None))
                .Verifiable();
            RunCommand("%s/foo/");
            _operations.Verify();
        }

        [Test]
        public void Substitute18()
        {
            Create("cat", "dog");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.ReportOnly));
            _operations.Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None));
            RunCommand("s");
        }

        [Test]
        public void Substitute19()
        {
            Create("cat", "dog");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.ReportOnly));
            _operations.Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll));
            RunCommand("s g");
        }

        [Test]
        public void Substitute20()
        {
            Create("cat", "dog");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.ReportOnly));
            _operations.Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll));
            RunCommand("& g");
        }

        [Test]
        [Description("Space in middle of flags is also not legal")]
        public void Substitute23()
        {
            Create("cat", "dog");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_TrailingCharacters)).Verifiable();
            RunCommand("s/a/b/& g");
            _factory.Verify();
        }

        [Test]
        [Description("Count is legal after the flags")]
        public void Substitute24()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 2), SubstituteFlags.None))
                .Verifiable();
            RunCommand("s/a/b/ 3");
            _factory.Verify();
        }

        [Test]
        [Description("Flags and count after the standard substitute command")]
        public void Substitute25()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 2), SubstituteFlags.OrdinalCase))
                .Verifiable();
            RunCommand("s/a/b/I 3");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat the last substitute when there is no last substitute")]
        public void Substitute26()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption<SubstituteData>.None;
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_NoPreviousSubstitute)).Verifiable();
            RunCommand("s");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat the substitute with no arguments should not re-use the flags")]
        public void Substitute27()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("s");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat the substitute with flags")]
        public void Substitute28()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("s g");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using & allows a space before the flags")]
        public void Substitute29()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("& g");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using & and several flags")]
        public void Substitute30()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.OrdinalCase | SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("&&g");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using & and a count")]
        public void Substitute31()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 2), SubstituteFlags.None))
                .Verifiable();
            RunCommand("& 3");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using ~ uses last search not the last substitute search pattern")]
        public void Substitute32()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.None));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("~");
            _factory.Verify();

        }

        [Test]
        [Description("Repeat using ~ does not repeat flags")]
        public void Substitute33()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.OrdinalCase));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("~");
            _factory.Verify();

        }

        [Test]
        [Description("Repeat using ~ allows flags after a space")]
        public void Substitute34()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.OrdinalCase));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("~ g");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using ~ allows for a count")]
        public void Substitute36()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.OrdinalCase));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 2), SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("~ g 3");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat using && needs to keep previous flags (single & does not)")]
        public void Substitute37()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.OrdinalCase))
                .Verifiable();
            RunCommand("&&");
            _factory.Verify();
        }

        [Test]
        [Description("Repeat via & plus r uses last search")]
        public void Substitute39()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.OrdinalCase));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("&r");
            _factory.Verify();
        }

        [Test]
        [Description("smagic has the additional Magic flag")]
        public void Substitute40()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Magic))
                .Verifiable();
            RunCommand("smagic/a/b");
            _factory.Verify();
            RunCommand("sm/a/b");
            _factory.Verify();
        }

        [Test]
        [Description("smagic with no arguments is a repeat")]
        public void Substitute41()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Magic))
                .Verifiable();
            RunCommand("smagic");
            _factory.Verify();
            RunCommand("sm");
            _factory.Verify();
        }

        [Test]
        [Description("snomagic has the additional Magic flag")]
        public void Substitute42()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Nomagic))
                .Verifiable();
            RunCommand("snomagic/a/b");
            _factory.Verify();
            RunCommand("sno/a/b");
            _factory.Verify();
        }

        [Test]
        [Description("snomagic with no arguments is a repeat")]
        public void Substitute43()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("a", "b", SubstituteFlags.OrdinalCase));
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.Nomagic))
                .Verifiable();
            RunCommand("snomagic");
            _factory.Verify();
            RunCommand("sno");
            _factory.Verify();
        }

        [Test]
        [Description("The print flag")]
        public void Substitute44()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.PrintLast))
                .Verifiable();
            RunCommand("s/a/b/p");
        }

        [Test]
        [Description("The print with number flag")]
        public void Substitute45()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.PrintLastWithNumber))
                .Verifiable();
            RunCommand("s/a/b/#");
        }

        [Test]
        [Description("The print with list flag")]
        public void Substitute46()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.PrintLastWithList))
                .Verifiable();
            RunCommand("s/a/b/l");
        }

        [Test]
        public void Substitute_EmptySearchUsesLastSearch()
        {
            Create("cat", "dog", "rabbit", "tree");
            _operations
                .Setup(x => x.Substitute("cat", "lab", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            _vimData.LastPatternData = VimUtil.CreatePatternData("cat", Path.Forward);
            RunCommand("s//lab/");
            _operations.Verify();
        }

        /// <summary>
        /// Support escaping of the backslash character in the strings
        /// </summary>
        [Test]
        public void Substitute_BackslashEscape()
        {
            Create("and");
            _operations
                .Setup(x => x.Substitute(@"and", @"and/or", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand(@"s/and/and\/or/");
            _operations.Verify();
        }

        /// <summary>
        /// Don't allow spaces between the flags
        /// </summary>
        [Test]
        public void Substitute_SpacesBetweenFlags()
        {
            Create("cat", "dog");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("cat", "dog", SubstituteFlags.None));
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_TrailingCharacters)).Verifiable();
            RunCommand("&& g");
            _factory.Verify();
        }

        /// <summary>
        /// Space between the final delimiter and flags is not allowed
        /// </summary>
        [Test]
        public void Substitute_SpaceBetweenFinalDelimiterAndFlags()
        {
            Create("cat", "dog");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_TrailingCharacters)).Verifiable();
            RunCommand("s/a/b/ g");
            _factory.Verify();
        }

        /// <summary>
        /// Spaces are not allowed in between flags.  They must come in a group
        /// </summary>
        [Test]
        public void Substitute_TildeWithSpacesInFlags()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("cat", "dog", SubstituteFlags.None));
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_TrailingCharacters)).Verifiable();
            RunCommand("~& g");
            _statusUtil.Verify();
        }

        /// <summary>
        /// No argument substitute + r should use the last pattern as the pattern
        /// </summary>
        [Test]
        public void Substitute_NoArgumentWithUseLastPatternFlag()
        {
            Create("cat", "dog", "rabbit", "tree");
            _vimData.LastSubstituteData = FSharpOption.Create(new SubstituteData("!!!", "b", SubstituteFlags.OrdinalCase));
            _vimData.LastPatternData = VimUtil.CreatePatternData("a");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("s r");
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we handle comma as a delimiter
        /// </summary>
        [Test]
        public void Substitute_CommaDelimiter()
        {
            Create("");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand("s,a,b,");
            _operations.Verify();
        }

        /// <summary>
        /// Make sure we handle ampersand as a delimiter
        /// </summary>
        [Test]
        public void Substitute_AmpersandDelimiter()
        {
            Create("");
            _operations
                .Setup(x => x.Substitute("a", "b", _textView.GetLineRange(0, 0), SubstituteFlags.ReplaceAll))
                .Verifiable();
            RunCommand("s&a&b&g");
            _operations.Verify();
        }

        /// <summary>
        /// Handle the case where the replace string consists only of back slashes.  Note: We don't want to interpret
        /// the backslashes here but instead want to pass them onto the substitute function.
        /// </summary>
        [Test]
        public void Substitute_ReplaceWithOnlyBackslashes()
        {
            Create("");
            _operations
                .Setup(x => x.Substitute("a", @"\\\\", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand(@":s/a/\\\\");
            _operations.Verify();
        }

        /// <summary>
        /// Handle the case where the replace string consists only of back slashes.  Note: We don't want to interpret
        /// the backslashes here but instead want to pass them onto the substitute function.
        /// </summary>
        [Test]
        public void Substitute_ReplaceWithOnlyBackslashesAndTrailingDelimiter()
        {
            Create("");
            _operations
                .Setup(x => x.Substitute("a", @"\\\\", _textView.GetLineRange(0, 0), SubstituteFlags.None))
                .Verifiable();
            RunCommand(@":s/a/\\\\/");
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

        /// <summary>
        /// By default the retab command should affect the entire ITextBuffer and not include
        /// space strings
        /// </summary>
        [Test]
        public void Retab_Default()
        {
            Create("cat", "dog");
            _commandOperations.Setup(x => x.RetabLineRange(_textBuffer.GetLineRange(0, 1), false)).Verifiable();
            RunCommand("retab");
            _factory.Verify();
        }

        /// <summary>
        /// The ! operator should force the command to include spaces
        /// </summary>
        [Test]
        public void Retab_WithBang()
        {
            Create("cat", "dog");
            _commandOperations.Setup(x => x.RetabLineRange(_textBuffer.GetLineRange(0, 1), true)).Verifiable();
            RunCommand("retab!");
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
            _commandOperations.Setup(x => x.PrintMarks(_buffer.Object.MarkMap)).Verifiable();
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
        public void Edit_NoArgumentsShouldReload()
        {
            Create("foo");
            _vimHost.Setup(x => x.Reload(_textBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            RunCommand("e");
            _operations.Verify();
            RunCommand("edi");
            _factory.Verify();
        }

        [Test]
        public void Edit_NoArgumentsButDirtyShouldError()
        {
            Create("");
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("e");
            _factory.Verify();
        }

        [Test]
        public void Edit_FilePathButDirtyShouldError()
        {
            Create("foo");
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("e cat.txt");
            _factory.Verify();
        }

        [Test]
        [Description("Can't figure out how to make this fail so just beeping now")]
        public void Edit_NoArgumentsReloadFailsShouldBeep()
        {
            Create("foo");
            _vimHost.Setup(x => x.Reload(_textBuffer)).Returns(false).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            _operations.Setup(x => x.Beep()).Verifiable();
            RunCommand("e");
            _factory.Verify();
        }

        [Test, Description("Make sure the starting e is not picked up as an :edit command")]
        public void Edit_BadCommandName()
        {
            Create("");
            _statusUtil.Setup(x => x.OnError(It.IsAny<string>())).Verifiable();
            RunCommand("endfunc");
            _statusUtil.Verify();
        }

        [Test]
        public void Edit_FilePathShouldLoadIntoExisting()
        {
            Create("");
            _vimHost.Setup(x => x.LoadFileIntoExistingWindow("cat.txt", _textBuffer)).Returns(HostResult.Success).Verifiable();
            _vimHost.Setup(x => x.IsDirty(_textBuffer)).Returns(false).Verifiable();
            RunCommand("e cat.txt");
            _factory.Verify();
        }

        [Test]
        public void Set1()
        {
            Create("bar");
            _commandOperations.Setup(x => x.PrintModifiedSettings()).Verifiable();
            RunCommand("se");
            _operations.Verify();
        }

        [Test]
        public void Set2()
        {
            Create("bar");
            _commandOperations.Setup(x => x.PrintModifiedSettings()).Verifiable();
            RunCommand("set");
            _commandOperations.Verify();
        }

        [Test]
        public void Set3()
        {
            Create("bar");
            _commandOperations.Setup(x => x.PrintAllSettings()).Verifiable();
            RunCommand("se all");
            _commandOperations.Verify();
        }

        [Test]
        public void Set4()
        {
            Create("bar");
            _commandOperations.Setup(x => x.PrintAllSettings()).Verifiable();
            RunCommand("set all");
            _commandOperations.Verify();
        }

        [Test]
        public void Set5()
        {
            Create("bar");
            _commandOperations.Setup(x => x.PrintSetting("foo")).Verifiable();
            RunCommand("set foo?");
            _commandOperations.Verify();
        }

        [Test]
        public void Set6()
        {
            Create("bar");
            _commandOperations.Setup(x => x.OperateSetting("foo")).Verifiable();
            RunCommand("set foo");
            _commandOperations.Verify();
        }

        [Test]
        public void Set7()
        {
            Create("bor");
            _commandOperations.Setup(x => x.ResetSetting("foo")).Verifiable();
            RunCommand("set nofoo");
            _commandOperations.Verify();
        }

        [Test]
        public void Set8()
        {
            Create("bar");
            _commandOperations.Setup(x => x.InvertSetting("foo")).Verifiable();
            RunCommand("set foo!");
            _commandOperations.Verify();
        }

        [Test]
        public void Set9()
        {
            Create("bar");
            _commandOperations.Setup(x => x.InvertSetting("foo")).Verifiable();
            RunCommand("set invfoo");
            _commandOperations.Verify();
        }

        [Test]
        public void Set10()
        {
            Create("bar");
            _commandOperations.Setup(x => x.SetSettingValue("foo", "bar")).Verifiable();
            RunCommand("set foo=bar");
            _commandOperations.Verify();
        }

        [Test]
        public void Set11()
        {
            Create("baa");
            _commandOperations.Setup(x => x.SetSettingValue("foo", "true")).Verifiable();
            RunCommand("set foo=true");
            _commandOperations.Verify();
        }

        [Test]
        public void Set12()
        {
            Create("baa");
            _commandOperations.Setup(x => x.SetSettingValue("foo", "true")).Verifiable();
            RunCommand("set foo:true");
            _commandOperations.Verify();
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
            _commandOperations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            RunCommand("source blah.txt");
            _commandOperations.Verify();
            _fileSystem.Verify();
        }

        [Test]
        public void Source4()
        {
            var text = new string[] { "set noignorecase", "set nofoo" };
            _fileSystem.Setup(x => x.ReadAllLines("blah.txt")).Returns(FSharpOption.Create(text)).Verifiable();
            _commandOperations.Setup(x => x.ResetSetting("ignorecase")).Verifiable();
            _commandOperations.Setup(x => x.ResetSetting("foo")).Verifiable();
            RunCommand("source blah.txt");
            _operations.Verify();
        }

        [Test, Description("RunCommand should strip off the : prefix")]
        public void RunCommand1()
        {
            var list = ListModule.OfSeq(":set nofoo");
            _commandOperations.Setup(x => x.ResetSetting("foo")).Verifiable();
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
            TestUnmap("xunm a ", "a", KeyRemapMode.Visual);
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
        public void PrintKeyMap_Map()
        {
            Create("");

            TestPrintMap("map", KeyRemapMode.Normal, KeyRemapMode.Visual, KeyRemapMode.OperatorPending);
            TestPrintMap("imap", KeyRemapMode.Insert);
            TestPrintMap("cmap", KeyRemapMode.Command);
            TestPrintMap("smap", KeyRemapMode.Select);
            TestPrintMap("nmap", KeyRemapMode.Normal);
            TestPrintMap("vmap", KeyRemapMode.Visual, KeyRemapMode.Select);
            TestPrintMap("xmap", KeyRemapMode.Visual);
        }

        /// <summary>
        /// Make sure the short name causes a save
        /// </summary>
        [Test]
        public void Write_ShortName()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textBuffer)).Returns(true).Verifiable();
            RunCommand("w");
            _vimHost.Verify();
        }

        /// <summary>
        /// Make sure the full name causes a save
        /// </summary>
        [Test]
        public void Write_FullName()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textBuffer)).Returns(true).Verifiable();
            RunCommand("write");
            _vimHost.Verify();
        }

        /// <summary>
        /// Providing an alternate name should cause the contents to be saved to a different
        /// file
        /// </summary>
        [Test]
        public void Write_DifferentName()
        {
            Create("cat");
            _vimHost.Setup(x => x.SaveTextAs("cat", "test.txt")).Returns(true).Verifiable();
            RunCommand("write test.txt");
            _vimHost.Verify();
        }

        /// <summary>
        /// Short name should still cause all buffers to be written
        /// </summary>
        [Test]
        public void WriteAll_ShortName()
        {
            Create("");
            var list = new List<IVimBuffer>() { _buffer.Object };
            _vim.SetupGet(x => x.Buffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.Save(_textBuffer)).Returns(true).Verifiable();
            RunCommand("wa");
            _factory.Verify();
        }

        [Test]
        public void WriteAll_FullName()
        {
            Create("");
            var list = new List<IVimBuffer>() { _buffer.Object };
            _vim.SetupGet(x => x.Buffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.Save(_textBuffer)).Returns(true).Verifiable();
            RunCommand("wall");
            _factory.Verify();
        }

        [Test]
        public void WriteQuit_NoArguments()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textView.TextBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand("wq");
            _factory.Verify();
        }

        [Test]
        public void WriteQuit_WithBang()
        {
            Create("");
            _vimHost.Setup(x => x.Save(_textView.TextBuffer)).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand("wq!");
            _factory.Verify();
        }

        [Test]
        public void WriteQuit_FileName()
        {
            Create("bar");
            _vimHost.Setup(x => x.SaveTextAs("bar", "foo.txt")).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand("wq foo.txt");
            _factory.Verify();
        }

        [Test]
        public void WriteQuit_Range()
        {
            Create("dog", "cat", "bear");
            _vimHost.Setup(x => x.SaveTextAs(It.IsAny<string>(), "foo.txt")).Returns(true).Verifiable();
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand("1,2wq foo.txt");
            _factory.Verify();
        }

        [Test]
        public void Quit1()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, true)).Verifiable();
            RunCommand("quit");
            _factory.Verify();
        }

        [Test]
        public void Quit2()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, true)).Verifiable();
            RunCommand("q");
            _factory.Verify();
        }

        [Test]
        public void Quit3()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand("q!");
            _factory.Verify();
        }

        /// <summary>
        /// When provided the ! bang option the application should just rudely exit
        /// </summary>
        [Test]
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
        [Test]
        public void QuitAll_WithNoDirty()
        {
            Create("");
            var buffer = _factory.Create<IVimBuffer>();
            buffer.SetupGet(x => x.TextBuffer).Returns(_factory.Create<ITextBuffer>().Object);
            var list = new List<IVimBuffer>() { buffer.Object };
            _vim.SetupGet(x => x.Buffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(false).Verifiable();
            _vimHost.Setup(x => x.Quit()).Verifiable();
            RunCommand("qall");
            _factory.Verify();
        }

        /// <summary>
        /// If there are dirty buffers and the ! option is missing then an error needs to be raised
        /// </summary>
        [Test]
        public void QuitAll_WithDirty()
        {
            Create("");
            var buffer = _factory.Create<IVimBuffer>();
            buffer.SetupGet(x => x.TextBuffer).Returns(_factory.Create<ITextBuffer>().Object);
            var list = new List<IVimBuffer>() { buffer.Object };
            _vim.SetupGet(x => x.Buffers).Returns(list.ToFSharpList()).Verifiable();
            _vimHost.Setup(x => x.IsDirty(It.IsAny<ITextBuffer>())).Returns(true).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.Common_NoWriteSinceLastChange)).Verifiable();
            RunCommand("qall");
            _factory.Verify();
        }

        [Test]
        public void TabNext_NoArguments()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Forward, 1)).Verifiable();
            RunCommand("tabnext");
            _operations.Verify();
        }

        [Test]
        public void TabNext_WithShortName()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Forward, 1)).Verifiable();
            RunCommand("tabn");
            _operations.Verify();
        }

        [Test]
        public void TabNext_WithCount()
        {
            Create("");
            _operations.Setup(x => x.GoToTab(3)).Verifiable();
            RunCommand("tabn 3");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious_NoArguments()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            RunCommand("tabprevious");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious_ShortName()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            RunCommand("tabp");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious_AlternateShortName()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            RunCommand("tabN");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious_AlternateFullName()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 1)).Verifiable();
            RunCommand("tabNext");
            _operations.Verify();
        }

        [Test]
        public void TabPrevious_AlternateNameAndCount()
        {
            Create("");
            _operations.Setup(x => x.GoToNextTab(Path.Backward, 42)).Verifiable();
            RunCommand("tabNext 42");
            _operations.Verify();
        }

        [Test]
        public void Split1()
        {
            Create("");
            _vimHost.Setup(x => x.SplitViewHorizontally(_textView)).Returns(HostResult.Success).Verifiable();
            RunCommand("split");
            _factory.Verify();
        }

        [Test]
        public void Split2()
        {
            Create("");
            _vimHost.Setup(x => x.SplitViewHorizontally(_textView)).Returns(HostResult.Success).Verifiable();
            RunCommand("sp");
            _factory.Verify();
        }

        [Test]
        public void Close1()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, true)).Verifiable();
            RunCommand(":close");
            _factory.Verify();
        }

        [Test]
        public void Close2()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand(":close!");
            _factory.Verify();
        }

        [Test]
        public void Close3()
        {
            Create("");
            _vimHost.Setup(x => x.Close(_textView, false)).Verifiable();
            RunCommand(":clo!");
            _factory.Verify();
        }

        [Test]
        public void Join_NoArguments()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("j");
            _factory.Verify();
        }

        [Test]
        public void Join_WithBang()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 1), JoinKind.KeepEmptySpaces))
                .Verifiable();
            RunCommand("j!");
            _factory.Verify();
        }

        [Test]
        public void Join_WithCount()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(0, 2), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("j 3");
            _factory.Verify();
        }

        [Test]
        public void Join_WithRangeAndCount()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(1, 3), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("2j 3");
            _factory.Verify();
        }

        [Test]
        [Description("Final count overrides the range and in case of 1 does nothing")]
        public void Join_WithRangeAndCountOfOne()
        {
            Create("dog", "cat", "tree", "rabbit");
            _operations
                .Setup(x => x.Join(_textView.GetLineRange(2, 2), JoinKind.RemoveEmptySpaces))
                .Verifiable();
            RunCommand("3j 1");
            _factory.Verify();
        }

        [Test]
        public void Range_CurrentLineWithIncrement()
        {
            Create("dog", "cat", "bear", "fish", "tree");
            TestRange(".,+2", _textView.GetLineRange(0, 2));
        }

    }
}
