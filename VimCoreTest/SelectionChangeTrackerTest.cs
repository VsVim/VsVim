using System;
using System.Collections.Generic;
using System.Threading;
using EditorUtils.UnitTest.Utils;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class SelectionChangeTrackerTest
    {
        private MockRepository _factory;
        private Mock<IVimBuffer> _vimBuffer;
        private Mock<ITextSelection> _selection;
        private Mock<ITextView> _textView;
        private Mock<IVisualModeSelectionOverride> _selectionOverride;
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
            _vimBuffer = MockObjectFactory.CreateVimBuffer(
                textView: _textView.Object,
                factory: _factory);

            _selectionOverride = _factory.Create<IVisualModeSelectionOverride>();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(It.IsAny<ITextView>())).Returns(false);
            var selectionList = new List<IVisualModeSelectionOverride>();
            selectionList.Add(_selectionOverride.Object);

            _context = new TestableSynchronizationContext();
            _context.Install();
            _tracker = new SelectionChangeTracker(_vimBuffer.Object, selectionList.ToFSharpList());
        }

        [TearDown]
        public void TearDown()
        {
            _context.Uninstall();
        }

        /// <summary>
        /// If we are already in visual mode then resync the selection
        /// </summary>
        [Test]
        public void SelectionChanged1()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, (object)null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        /// <summary>
        /// If there is no actual selection then there is nothing to do
        /// </summary>
        [Test]
        public void SelectionChanged2()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        public void SelectionChanged3()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _vimBuffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _context.RunAll();
            _factory.Verify();
        }

        /// <summary>
        /// Make sure that the selection is still valid when the post occurs. If it 
        /// it resets then there is nothing to do
        /// </summary>
        [Test]
        public void SelectionChanged4()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _vimBuffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Throws(new Exception());
            _context.RunAll();
            _factory.Verify();
        }

        /// <summary>
        /// Selection changes while in visual mode should reset the selection if they weren't
        /// actually caused by visual mode
        /// </summary>
        [Test]
        public void SelectionChanged5()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        [Test]
        public void SelectionChanged6()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
            _factory.Verify();

            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _vimBuffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _context.RunAll();
            _factory.Verify();
        }

        /// <summary>
        /// If the selection is empty then there is no reason to switch out
        /// </summary>
        [Test]
        public void SelectionChanged7()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
            _factory.Verify();
        }

        /// <summary>
        /// Don't switch from visual character to visual line if the selection changes
        /// </summary>
        [Test]
        public void SelectionChanged8()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
        }

        /// <summary>
        /// Don't switch from visual line to visual character if the selection changes
        /// </summary>
        [Test]
        public void SelectionChanged9()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualLine).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
        }

        /// <summary>
        /// Make sure that we handle the case where the synchronization context isn't 
        /// set
        /// </summary>
        [Test]
        public void BadSynchronizationContext()
        {
            SynchronizationContext.SetSynchronizationContext(null);
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _vimBuffer
                .Setup(x => x.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None))
                .Returns(_factory.Create<IMode>().Object)
                .Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
            Assert.IsTrue(_context.IsEmpty);     // Shouldn't be accessible
        }

        /// <summary>
        /// Let the IVisualModeSelectionOverride prevent a transition out of insert
        /// mode into visual
        /// </summary>
        [Test]
        public void OverrideCanPreventTransition()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(_textView.Object)).Returns(true);
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsTrue(_context.IsEmpty);
        }

        /// <summary>
        /// IVisualModeSelectionOverride is only used for overriding transitions out of insert
        /// mode.  Not relevant for other mode kinds
        /// </summary>
        [Test]
        public void OverrideOnlyMattersForInsertMode()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(_textView.Object)).Returns(true);
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.IsFalse(_context.IsEmpty);
        }
    }
}
