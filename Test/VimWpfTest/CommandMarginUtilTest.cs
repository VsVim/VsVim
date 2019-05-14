using System.Linq;
using Microsoft.FSharp.Collections;
using Moq;
using Vim.EditorHost;
using Vim.UI.Wpf.Implementation.CommandMargin;
using Vim.UnitTest;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public class CommandMarginUtilTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly Mock<IIncrementalSearch> _search;
        private readonly MockVimBuffer _vimBuffer;
        private readonly Mock<ICommandRunner> _normalRunner;
        private readonly Mock<INormalMode> _normalMode;
        private readonly Mock<IVisualMode> _visualMode;
        private readonly Mock<ICommandRunner> _visualRunner;

        public CommandMarginUtilTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            
            _search = _factory.Create<IIncrementalSearch>();
            _search.SetupGet(x => x.HasActiveSession).Returns(false);
            
            _normalMode = _factory.Create<INormalMode>();
            _normalMode.SetupGet(x => x.Command).Returns(string.Empty);
            _normalRunner = _factory.Create<ICommandRunner>();
            _normalMode.SetupGet(x => x.CommandRunner).Returns(_normalRunner.Object);
            _normalRunner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);

            _visualMode = _factory.Create<IVisualMode>();
            _visualRunner = _factory.Create<ICommandRunner>();
            _visualMode.SetupGet(x => x.CommandRunner).Returns(_visualRunner.Object);
            _visualRunner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
            
            _vimBuffer = new MockVimBuffer
                         {
                                 IncrementalSearchImpl = _search.Object,
                                 VimImpl = MockObjectFactory.CreateVim(factory: _factory).Object,
                                 NormalModeImpl = _normalMode.Object,
                                 BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty
                         };
        }

        [WpfFact]
        public void GetCommandStatusNormalMode()
        {
            const string partialCommand = "di";
            _normalMode.SetupGet(x => x.Command).Returns(partialCommand);
            _vimBuffer.ModeKindImpl = ModeKind.Normal;

            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusNormalModeBufferedKeys()
        {
            const string partialCommand = @"\s";
            _vimBuffer.BufferedKeyInputsImpl = ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput));
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusNormalModeCommandInputs()
        {
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            
            const string partialCommand1 = "\u0017g"; // <C-w>g
            _normalRunner.SetupGet(x => x.Inputs).Returns(ListModule.OfSeq(partialCommand1.Select(KeyInputUtil.CharToKeyInput)));
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal("^Wg", actual);
            
            const string partialCommand2 = "\u0017\u0007"; // <C-w><C-g>
            _normalRunner.SetupGet(x => x.Inputs).Returns(ListModule.OfSeq(partialCommand2.Select(KeyInputUtil.CharToKeyInput)));
            actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal("^W^G", actual);
        }
        
        [WpfFact]
        public void GetCommandStatusNormalModeCommandInputsAndBufferedKeys()
        {
            const string commandInputs = "2c";
            const string bufferedInputs = "2t";
            _normalRunner.SetupGet(x => x.Inputs).Returns(ListModule.OfSeq(commandInputs.Select(KeyInputUtil.CharToKeyInput)));
            _vimBuffer.BufferedKeyInputsImpl = ListModule.OfSeq(bufferedInputs.Select(KeyInputUtil.CharToKeyInput));
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(commandInputs + bufferedInputs, actual);
        }

        [WpfFact]
        public void GetCommandStatusVisualCharacterMode()
        {
            string[] lines = { "cat", "dog", "fish" };
            var textBuffer = CreateTextBuffer(lines);

            _vimBuffer.TextBufferImpl = textBuffer;
            _vimBuffer.ModeImpl = _visualMode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            // span1 selection is within one line, so the display should be the selected character count
            var span1 = new CharacterSpan(textBuffer.GetPointInLine(1, 1), textBuffer.GetPointInLine(1, 3));
            var selection1 = new VisualSelection.Character(span1, SearchPath.Forward);
            _visualMode.SetupGet(x => x.VisualSelection).Returns(selection1);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));
            
            // span2 selection is over two lines, so the the display should be the selected line count
            var span2 = new CharacterSpan(textBuffer.GetPointInLine(0, 1), textBuffer.GetPointInLine(1, 2));
            var selection2 = new VisualSelection.Character(span2, SearchPath.Forward);
            _visualMode.SetupGet(x => x.VisualSelection).Returns(selection2);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));

            // span3 selection is within one line and includes the CRLF at the end of the line, so the the display should be the length of the line + 1
            var span3 = new CharacterSpan(textBuffer.GetPointInLine(1, 1), textBuffer.GetPointInLine(1, lines[1].Length + 2));
            var selection3 = new VisualSelection.Character(span3, SearchPath.Forward);
            _visualMode.SetupGet(x => x.VisualSelection).Returns(selection3);
            Assert.Equal("3", CommandMarginUtil.GetShowCommandText(_vimBuffer));
        }
        
        [WpfFact]
        public void GetCommandStatusVisualBlockMode()
        {
            const int expectedLineCount = 2;
            const int expectedColumnCount = 3;

            var textBuffer = CreateTextBuffer("cat", "dog", "fish");
            var span = new BlockSpan(textBuffer.GetPoint(0), 4, expectedColumnCount, expectedLineCount);
            var selection = new VisualSelection.Block(span, BlockCaretLocation.TopLeft);

            _vimBuffer.BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty;
            _visualMode.SetupGet(x => x.VisualSelection).Returns(selection);

            _vimBuffer.ModeImpl = _visualMode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualBlock;
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal($"{expectedLineCount}x{expectedColumnCount}", actual);
        }
        
        [WpfFact]
        public void GetCommandStatusVisualLineMode()
        {
            string[] lines = { "cat", "dog", "fish" };
            var textBuffer = CreateTextBuffer(lines);
            
            _vimBuffer.TextBufferImpl = textBuffer;
            _vimBuffer.ModeImpl = _visualMode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualLine;
            
            var span = new SnapshotLineRange(textBuffer.CurrentSnapshot, 1, 2);
            var selection = new VisualSelection.Line(span, SearchPath.Forward, 0);
            _visualMode.SetupGet(x => x.VisualSelection).Returns(selection);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));
        }
        
        [WpfFact]
        public void GetCommandStatusVisualModeCommandInputs()
        {
            const string partialCommand = @"3f";
            _visualRunner.SetupGet(x => x.Inputs).Returns(ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput)));
            
            _vimBuffer.ModeImpl = _visualMode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusVisualModeBufferedKeys()
        {
            const string partialCommand = @"\e";
            
            _vimBuffer.BufferedKeyInputsImpl = ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput));
            _vimBuffer.ModeImpl = _visualMode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
    }
}
