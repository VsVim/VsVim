using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class ChangeTrackerTest
    {
        private MockRepository _factory;
        private ChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ITextChangeTrackerFactory> _textChangeTrackerFactory;
        private Mock<ICommandRunner> _runner;
        private Mock<ITextChangeTracker> _textChangeTracker;
        private Mock<IVimBuffer> _buffer;
        private Mock<INormalMode> _normalMode;
        private IVimData _vimData;

        private void CreateForText(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateTextBuffer(lines);
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

            // Setup the ITextChangeTrackerFactory to give back our ITextChangeTracker 
            // for the IVimBuffer
            _textChangeTracker = _factory.Create<ITextChangeTracker>(MockBehavior.Loose);
            _textChangeTrackerFactory = _factory.Create<ITextChangeTrackerFactory>();
            _textChangeTrackerFactory.Setup(x => x.GetTextChangeTracker(It.IsAny<IVimBuffer>())).Returns(_textChangeTracker.Object);

            _vimData = new VimData();
            var vim = MockObjectFactory.CreateVim(vimData: _vimData);
            _tracker = new ChangeTracker(_textChangeTrackerFactory.Object, vim.Object);
            _tracker.OnVimBufferCreated(_buffer.Object);
        }

        [Test]
        public void LinkedWithTextChange_Simple()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.LinkedWithNextTextChange | CommandFlags.Repeatable);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
            _textChangeTracker.Raise(x => x.ChangeCompleted += null, (object) null, TextChange.NewInsert("foo"));
            var last = _vimData.LastCommand;
            Assert.IsTrue(last.IsSome(x => x.IsLinkedCommand));
        }

        /// <summary>
        /// Don't track commands which are not repeatable
        /// </summary>
        [Test]
        public void OnCommand_NotRepetable()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.None);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            Assert.IsTrue(_vimData.LastCommand.IsNone());
        }

        /// <summary>
        /// Definitely track repeatable changes
        /// </summary>
        [Test]
        public void OnCommand_Repeatable()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Repeatable);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            Assert.IsTrue(_vimData.LastCommand.IsSome(x => x.IsNormalCommand));
        }

        /// <summary>
        /// Don't track movement commands.  They don't get repeated
        /// </summary>
        [Test]
        public void OnCommand_DontTrackMovement()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Movement);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            Assert.IsTrue(_vimData.LastCommand.IsNone());
        }

        /// <summary>
        /// Don't track special commands.  They don't get repeated
        /// </summary>
        [Test]
        public void OnCommand_DontTrackSpecial()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Special);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            Assert.IsTrue(_vimData.LastCommand.IsNone());
        }

        /// <summary>
        /// Track text changes 
        /// </summary>
        [Test]
        public void OnTextChange_Standard()
        {
            CreateForText("hello");
            _textChangeTracker.Raise(x => x.ChangeCompleted += null, (object) null, TextChange.NewInsert("foo"));
            Assert.IsTrue(_vimData.LastCommand.IsSome(x => x.IsTextChangeCommand));
        }

        /// <summary>
        /// A text change should override a normal command change
        /// </summary>
        [Test]
        public void OnTextChange2()
        {
            CreateForText("hello");
            var data = VimUtil.CreateCommandRunData(flags: CommandFlags.Repeatable);
            _runner.Raise(x => x.CommandRan += null, (object) null, data);
            _textChangeTracker.Raise(x => x.ChangeCompleted += null, (object) null, TextChange.NewInsert("foo"));
            Assert.IsTrue(_vimData.LastCommand.IsSome(x => x.IsTextChangeCommand));
        }

    }
}
