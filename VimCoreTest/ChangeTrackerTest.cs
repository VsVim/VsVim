using System;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class ChangeTrackerTest
    {
        private MockRepository _factory;
        private ChangeTracker _trackerRaw;
        private IChangeTracker _tracker;
        private ITextBuffer _textBuffer;
        private Mock<ITextView> _textView;
        private Mock<ITextChangeTrackerFactory> _textChangeTrackerFactory;
        private MockVimBuffer _buffer;
        private MockNormalMode _normalMode;
        private MockCommandRunner _normalModeRunner;
        private MockTextChangeTracker _textChangeTracker;


        private void CreateForText(params string[] lines)
        {
            _textBuffer = EditorUtil.CreateBuffer(lines);
            _textView = MockObjectFactory.CreateTextView(_textBuffer);
            _textView.SetupGet(x => x.HasAggregateFocus).Returns(true);

            _normalModeRunner = new MockCommandRunner();
            _normalMode = new MockNormalMode() { CommandRunnerImpl = _normalModeRunner };
            _buffer = new MockVimBuffer() { TextViewImpl = _textView.Object, TextBufferImpl = _textBuffer, NormalModeImpl = _normalMode };
            _textChangeTracker = new MockTextChangeTracker() { VimBufferImpl = _buffer };

            _factory = new MockRepository(MockBehavior.Loose);
            _factory.DefaultValue = DefaultValue.Mock;
            _textChangeTrackerFactory = _factory.Create<ITextChangeTrackerFactory>();
            _textChangeTrackerFactory.Setup(x => x.GetTextChangeTracker(_buffer)).Returns(_textChangeTracker);
            _buffer.VisualBlockModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualCharacterModeImpl = _factory.Create<IVisualMode>().Object;
            _buffer.VisualLineModeImpl = _factory.Create<IVisualMode>().Object;
            _trackerRaw = new ChangeTracker(_textChangeTrackerFactory.Object);
            _tracker = _trackerRaw;
            _trackerRaw.OnVimBufferCreated(_buffer);
        }

        private CommandRunData CreateCommand(
            Func<FSharpOption<int>, Register, CommandResult> func = null,
            KeyInputSet name = null,
            CommandFlags? flags = null,
            int? count = 0,
            MotionRunData motionRunData = null,
            VisualSpan visualRunData = null)
        {
            name = name ?? KeyInputSet.NewOneKeyInput(KeyInputUtil.CharToKeyInput('c'));
            var flagsRaw = flags ?? CommandFlags.None;
            var countRaw = count.HasValue ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            var funcRaw = func.ToFSharpFunc();
            var cmd = Command.NewSimpleCommand(
                name,
                flagsRaw,
                func.ToFSharpFunc());
            return new CommandRunData(
                cmd,
                new Register('c'),
                countRaw,
                motionRunData != null ? FSharpOption.Create(motionRunData) : FSharpOption<MotionRunData>.None,
                visualRunData != null ? FSharpOption.Create(visualRunData) : FSharpOption<VisualSpan>.None);
        }

        private CommandResult CreateResult()
        {
            return CommandResult.NewCompleted(ModeSwitch.NewSwitchMode(ModeKind.Insert));
        }

        [Test]
        public void LinkedWithTextChange1()
        {
            CreateForText("hello");
            var res = CommandResult.NewCompleted(ModeSwitch.NewSwitchMode(ModeKind.Insert));
            _normalModeRunner.RaiseCommandRan(
                CreateCommand(flags: CommandFlags.LinkedWithNextTextChange | CommandFlags.Repeatable),
                res);
            _buffer.ModeKindImpl = ModeKind.Insert;
            _textChangeTracker.RaiseChangeCompleted("foo");
            var last = _tracker.LastChange.Value;
            Assert.IsTrue(last.IsLinkedChange);
        }

        [Test]
        [Description("Don't track a command unless it's repeatable")]
        public void OnCommand1()
        {
            CreateForText("hello");
            var cmd = CreateCommand(flags: CommandFlags.None);
            _normalModeRunner.RaiseCommandRan(cmd, CreateResult());
            Assert.IsTrue(_tracker.LastChange.IsNone());
        }

        [Test]
        public void OnCommand2()
        {
            CreateForText("hello");
            var cmd = CreateCommand(flags: CommandFlags.Repeatable);
            _normalModeRunner.RaiseCommandRan(cmd, CreateResult());
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.IsTrue(_tracker.LastChange.Value.IsCommandChange);
        }

        [Test]
        [Description("Don't track movement commands")]
        public void OnCommand3()
        {
            CreateForText("hello");
            var cmd = CreateCommand(flags: CommandFlags.Movement);
            _normalModeRunner.RaiseCommandRan(cmd, CreateResult());
            Assert.IsTrue(_tracker.LastChange.IsNone());
        }

        [Test]
        [Description("Don't track special commands")]
        public void OnCommand4()
        {
            CreateForText("hello");
            var cmd = CreateCommand(flags: CommandFlags.Special);
            _normalModeRunner.RaiseCommandRan(cmd, CreateResult());
            Assert.IsTrue(_tracker.LastChange.IsNone());
        }

        [Test]
        public void OnTextChange1()
        {
            CreateForText("hello");
            _textChangeTracker.RaiseChangeCompleted("foo");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("foo", _tracker.LastChange.Value.AsTextChange().Item);
        }

        [Test]
        [Description("Should replace a normal change")]
        public void OnTextChange2()
        {
            CreateForText("hello");
            var cmd = CreateCommand(flags: CommandFlags.Repeatable);
            _normalModeRunner.RaiseCommandRan(cmd, CreateResult());
            _textChangeTracker.RaiseChangeCompleted("foo");
            Assert.IsTrue(_tracker.LastChange.IsSome());
            Assert.AreEqual("foo", _tracker.LastChange.Value.AsTextChange().Item);
        }

    }
}
