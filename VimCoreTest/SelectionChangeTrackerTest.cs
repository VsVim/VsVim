using System;
using System.Threading;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class SelectionChangeTrackerTest
    {
        private MockRepository _factory;
        private Mock<IVimBuffer> _buffer;
        private Mock<ITextSelection> _selection;
        private Mock<ITextView> _textView;
        private TestableSynchronizationContext _context;
        private SelectionChangeTracker _tracker;

        [SetUp]
        public void Setup()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _selection = _factory.Create<ITextSelection>();
            _textView = MockObjectFactory.CreateTextView(
                selection: _selection.Object,
                factory: _factory);
            _buffer = MockObjectFactory.CreateVimBuffer(
                view: _textView.Object,
                factory: _factory);
            _context = new TestableSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_context);
            _tracker = new SelectionChangeTracker(_buffer.Object);
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [Test]
        [Description("Already in Visual Mode must resync the selection")]
        public void SelectionChanged1()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _buffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, (object)null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        [Description("Not in Visual Mode but there is no selection so nothing to do")]
        public void SelectionChanged2()
        {
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        public void SelectionChanged3()
        {
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _buffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _context.RunAll();
            _factory.Verify();
        }

        [Test]
        [Description("Make sure the selection is still valid when the post occurs")]
        public void SelectionChanged4()
        {
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _buffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Throws(new Exception());
            _context.RunAll();
            _factory.Verify();
        }

        [Test]
        [Description("Selection changes while in Visual Mode should reset the selection")]
        public void SelectionChanged5()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _buffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        [Description("Selection changes while in Visual Mode should reset the mode")]
        public void SelectionChanged6()
        {
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _buffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _context.RunAll();
            _factory.Verify();
        }

        [Test]
        [Description("Don't switch out of non-normal mode")]
        public void SelectionChanged7()
        {
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        [Description("Visual Studio does not guarantee the SynchronizationContext is set")]
        public void BadSynchronizationContext()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            _buffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _buffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _buffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
            Assert.IsTrue(_context.IsEmpty);     // Shouldn't be accessible
        }


    }
}
