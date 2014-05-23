using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class ChangeTrackerTest : VimTestBase
    {
        private MockRepository _factory;
        private ChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ICommandRunner> _runner;
        private Mock<IVimBuffer> _buffer;
        private Mock<INormalMode> _normalMode;
        private IVimData _vimData;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
            _textView = MockObjectFactory.CreateTextView(_textBuffer);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);

            // Setup normal mode so that we can provide an ICommandRunner to 
            // recieve commands from
            _factory = new MockRepository(MockBehavior.Loose) {DefaultValue = DefaultValue.Mock};
            _runner = _factory.Create<ICommandRunner>(MockBehavior.Loose);
            _normalMode = _factory.Create<INormalMode>(MockBehavior.Strict);
            _normalMode.SetupGet(x => x.CommandRunner).Returns(_runner.Object);

            // Create the IVimBuffer instance
            _buffer = _factory.Create<IVimBuffer>(MockBehavior.Loose);
            _buffer.DefaultValue = DefaultValue.Mock;
            _buffer.SetupGet(x => x.NormalMode).Returns(_normalMode.Object);

            _vimData = VimUtil.CreateVimData();
            var vim = MockObjectFactory.CreateVim(vimData: _vimData);
            _tracker = new ChangeTracker(vim.Object);
            _tracker.OnVimBufferCreated(_buffer.Object);
        }

        /// <summary>
        /// Make sure a commands which are marked as LinkWithNextCommand do link with the next command
        /// </summary>
        [Fact]
        public void LinkedWithNextChange_Simple()
        {
            Create("hello");
            var runData1 = VimUtil.CreateCommandRunData(flags: CommandFlags.LinkedWithNextCommand | CommandFlags.Repeatable);
            var runData2 = VimUtil.CreateCommandRunData(flags: CommandFlags.Repeatable, command: Command.NewInsertCommand(InsertCommand.NewInsert("foo")));
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(runData1));
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(runData2));
            var lastCommnad = _vimData.LastCommand;
            Assert.True(lastCommnad.IsSome(x => x.IsLinkedCommand));
        }

        /// <summary>
        /// Don't track commands which are not repeatable
        /// </summary>
        [Fact]
        public void OnCommand_NotRepetable()
        {
            Create("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.None);
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(data));
            Assert.True(_vimData.LastCommand.IsNone());
        }

        /// <summary>
        /// Definitely track repeatable changes
        /// </summary>
        [Fact]
        public void OnCommand_Repeatable()
        {
            Create("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Repeatable);
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(data));
            Assert.True(_vimData.LastCommand.IsSome(x => x.IsNormalCommand));
        }

        /// <summary>
        /// Don't track movement commands.  They don't get repeated
        /// </summary>
        [Fact]
        public void OnCommand_DontTrackMovement()
        {
            Create("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Movement);
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(data));
            Assert.True(_vimData.LastCommand.IsNone());
        }

        /// <summary>
        /// Don't track special commands.  They don't get repeated
        /// </summary>
        [Fact]
        public void OnCommand_DontTrackSpecial()
        {
            Create("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Special);
            _runner.Raise(x => x.CommandRan += null, (object) null, new CommandRunDataEventArgs(data));
            Assert.True(_vimData.LastCommand.IsNone());
        }
    }
}
