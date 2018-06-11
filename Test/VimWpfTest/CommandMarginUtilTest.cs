using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.Text;
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

        public CommandMarginUtilTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            
            _search = _factory.Create<IIncrementalSearch>();
            
            _vimBuffer = new MockVimBuffer
            {
                IncrementalSearchImpl = _search.Object,
                VimImpl = MockObjectFactory.CreateVim(factory: _factory).Object,
                CommandModeImpl = _factory.Create<ICommandMode>(MockBehavior.Loose).Object
            };
        }

        [WpfFact]
        public void GetCommandStatusNormalMode()
        {
            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<INormalMode>();
            const string partialCommand = "di";
            mode.SetupGet(x => x.Command).Returns(partialCommand);
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusNormalModeBufferedKeys()
        {
            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<INormalMode>();
            const string partialCommand = @"\s";
            mode.SetupGet(x => x.Command).Returns(string.Empty);
            
            _vimBuffer.BufferedKeyInputsImpl = ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput));
            _vimBuffer.NormalModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.Normal;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusVisualCharacterMode()
        {
            string[] lines = { "cat", "dog", "fish" };
            var textBuffer = CreateTextBuffer(lines);

            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<IVisualMode>();
            var runner = _factory.Create<ICommandRunner>();
            runner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
            mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);

            _vimBuffer.TextBufferImpl = textBuffer;
            _vimBuffer.BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty;
            _vimBuffer.ModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            // span1 selection is within one line, so the display should be the selected character count
            var span1 = new CharacterSpan(textBuffer.GetPointInLine(1, 1), textBuffer.GetPointInLine(1, 3));
            var selection1 = new VisualSelection.Character(span1, SearchPath.Forward);
            mode.SetupGet(x => x.VisualSelection).Returns(selection1);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));
            
            // span2 selection is over two lines, so the the display should be the selected line count
            var span2 = new CharacterSpan(textBuffer.GetPointInLine(0, 1), textBuffer.GetPointInLine(1, 2));
            var selection2 = new VisualSelection.Character(span2, SearchPath.Forward);
            mode.SetupGet(x => x.VisualSelection).Returns(selection2);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));

            // span3 selection is within one line and includes the CRLF at the end of the line, so the the display should be the length of the line + 1
            var span3 = new CharacterSpan(textBuffer.GetPointInLine(1, 1), textBuffer.GetPointInLine(1, lines[1].Length + 2));
            var selection3 = new VisualSelection.Character(span3, SearchPath.Forward);
            mode.SetupGet(x => x.VisualSelection).Returns(selection3);
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

            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<IVisualMode>();
            var runner = _factory.Create<ICommandRunner>();
            runner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
            _vimBuffer.BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty;
            mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);
            mode.SetupGet(x => x.VisualSelection).Returns(selection);

            _vimBuffer.ModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualLine;
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal($"{expectedLineCount}x{expectedColumnCount}", actual);
        }
        
        [WpfFact]
        public void GetCommandStatusVisualLineMode()
        {
            string[] lines = { "cat", "dog", "fish" };
            var textBuffer = CreateTextBuffer(lines);
            
           const int expectedLineCount = 23;
            
            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<IVisualMode>();
            var runner = _factory.Create<ICommandRunner>();
            runner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
            mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);

            _vimBuffer.TextBufferImpl = textBuffer;
            _vimBuffer.BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty;
            _vimBuffer.ModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualLine;
            
            var span = new CharacterSpan(textBuffer.GetPointInLine(1, 1), textBuffer.GetPointInLine(2, 3));
            var selection = new VisualSelection.Character(span, SearchPath.Forward);
            mode.SetupGet(x => x.VisualSelection).Returns(selection);
            Assert.Equal("2", CommandMarginUtil.GetShowCommandText(_vimBuffer));
        }
        
        [WpfFact]
        public void GetCommandStatusVisualModeCommandInputs()
        {
            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<IVisualMode>();
            var runner = _factory.Create<ICommandRunner>();
            const string partialCommand = @"3f";
            mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);
            runner.SetupGet(x => x.Inputs).Returns(ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput)));
            
            _vimBuffer.ModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
        
        [WpfFact]
        public void GetCommandStatusVisualModeBufferedKeys()
        {
            _search.SetupGet(x => x.InSearch).Returns(false);
            var mode = _factory.Create<IVisualMode>();
            var runner = _factory.Create<ICommandRunner>();
            const string partialCommand = @"\e";
            mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);
            runner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
            
            _vimBuffer.BufferedKeyInputsImpl = ListModule.OfSeq(partialCommand.Select(KeyInputUtil.CharToKeyInput));
            _vimBuffer.ModeImpl = mode.Object;
            _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
            
            var actual = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            Assert.Equal(partialCommand, actual);
        }
    }
}
