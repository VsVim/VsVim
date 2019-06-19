using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Vim.Extensions;
using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class SelectionChangeTrackerTest : IDisposable
    {
        private readonly MockRepository _factory;
        private readonly Mock<IVimBuffer> _vimBuffer;
        private readonly Mock<IVimHost> _vimHost;
        private readonly Mock<ITextSelection> _selection;
        private readonly Mock<ITextView> _textView;
        private readonly Mock<IVisualModeSelectionOverride> _selectionOverride;
        private readonly Mock<IMouseDevice> _mouseDevice;
        private readonly TestableSynchronizationContext _context;
        private readonly SelectionChangeTracker _tracker;
        private readonly VirtualSnapshotSpan _nonEmptySpan;

        public SelectionChangeTrackerTest()
        {
            _factory = new MockRepository(MockBehavior.Loose);
            _selection = _factory.Create<ITextSelection>();
            _textView = MockObjectFactory.CreateTextView(
                selection: _selection.Object,
                factory: _factory);
            _vimHost = _factory.Create<IVimHost>();
            _vimHost.Setup(x => x.IsFocused(_textView.Object)).Returns(true);
            _vimBuffer = MockObjectFactory.CreateVimBuffer(
                textView: _textView.Object,
                vim: MockObjectFactory.CreateVim(host: _vimHost.Object).Object,
                factory: _factory);
            _vimBuffer.SetupGet(x => x.IsClosed).Returns(false);

            var snapshot = _factory.Create<ITextSnapshot>();
            snapshot.SetupGet(x => x.Length).Returns(1);
            _nonEmptySpan = new VirtualSnapshotSpan(new SnapshotSpan(snapshot.Object, new Span(0, 1)));

            _mouseDevice = _factory.Create<IMouseDevice>();
            _selectionOverride = _factory.Create<IVisualModeSelectionOverride>();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(It.IsAny<ITextView>())).Returns(false);
            var selectionList = new List<IVisualModeSelectionOverride>
            {
                _selectionOverride.Object
            };
            _context = new TestableSynchronizationContext();

            // Mock 'DoActionAsync' in 'ICommonOperations'.
            var commonOperations = _factory.Create<ICommonOperations>();
            commonOperations.Setup(x => x.DoActionAsync(It.IsAny<FSharpFunc<Unit, Unit>>()))
                .Callback((FSharpFunc<Unit, Unit> action) => _context.Post(_ => action.Invoke(null), null));

            _tracker = new SelectionChangeTracker(
                _vimBuffer.Object,
                commonOperations.Object,
                selectionList.ToFSharpList(),
                _mouseDevice.Object);
        }

        public void Dispose()
        {
            _context.Dispose();
        }

        /// <summary>
        /// If we are already in visual mode then resync the selection
        /// </summary>
        [Fact]
        public void SelectionChanged1()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, (object)null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
            _factory.Verify();
        }

        /// <summary>
        /// If there is no actual selection then there is nothing to do
        /// </summary>
        [Fact]
        public void SelectionChanged2()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
            _factory.Verify();
        }

        [Fact]
        public void SelectionChanged3()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.False(_context.IsEmpty);
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
        [Fact]
        public void SelectionChanged4()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.False(_context.IsEmpty);
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
        [Fact]
        public void SelectionChanged5()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
            _factory.Verify();
        }

        [Fact]
        public void SelectionChanged6()
        {
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.False(_context.IsEmpty);
            _factory.Verify();

            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
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
        [Fact]
        public void SelectionChanged7()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(true).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
            _factory.Verify();
        }

        /// <summary>
        /// Don't switch from visual character to visual line if the selection changes
        /// </summary>
        [Fact]
        public void SelectionChanged8()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
        }

        /// <summary>
        /// Don't switch from visual line to visual character if the selection changes
        /// </summary>
        [Fact]
        public void SelectionChanged9()
        {
            var mode = _factory.Create<IVisualMode>();
            mode.Setup(x => x.SyncSelection()).Verifiable();
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualLine).Verifiable();
            _vimBuffer.SetupGet(x => x.Mode).Returns(mode.Object).Verifiable();
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            _factory.Verify();
        }

        /// <summary>
        /// Make sure we gracefully handle the case where the IVimBuffer is closed in between
        /// the post of the synchronization set and the actual running of the callback
        /// </summary>
        [Fact]
        public void BufferClosedDuringPost()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false);
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
            _selection.SetupGet(x => x.IsEmpty).Returns(false);
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.SetupGet(x => x.Mode).Returns(TextSelectionMode.Stream);
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);

            _vimBuffer.SetupGet(x => x.IsClosed).Returns(true).Verifiable();
            _vimBuffer.Setup(x => x.SwitchMode(It.IsAny<ModeKind>(), It.IsAny<ModeArgument>())).Throws(new Exception());
            _context.RunAll();
            _factory.Verify();
        }

        /// <summary>
        /// Let the IVisualModeSelectionOverride prevent a transition out of insert
        /// mode into visual
        /// </summary>
        [Fact]
        public void OverrideCanPreventTransition()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert).Verifiable();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(_textView.Object)).Returns(true);
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
        }

        /// <summary>
        /// IVisualModeSelectionOverride is only used for overriding transitions out of insert
        /// mode.  Not relevant for other mode kinds
        /// </summary>
        [Fact]
        public void OverrideOnlyMattersForInsertMode()
        {
            _vimBuffer.SetupGet(x => x.IsProcessingInput).Returns(false).Verifiable();
            _vimBuffer.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal).Verifiable();
            _selectionOverride.Setup(x => x.IsInsertModePreferred(_textView.Object)).Returns(true);
            _selection.SetupGet(x => x.IsEmpty).Returns(false).Verifiable();
            _selection.SetupGet(x => x.StreamSelectionSpan).Returns(_nonEmptySpan).Verifiable();
            _selection.Raise(x => x.SelectionChanged += null, null, EventArgs.Empty);
            Assert.False(_context.IsEmpty);
            _context.RunAll();
        }

        /// <summary>
        /// The selection change tracker should only be dealing with the ITextView which is actually
        /// being used by the user.  It's possible for a single ITextBuffer to have multiple ITextView
        /// instances and edits from one can affect anothers selection.  Hence we should only process the
        /// one which has aggregate focus
        /// </summary>
        [Fact]
        public void HasAggregateFocus()
        {
            _vimHost.Setup(x => x.IsFocused(_textView.Object)).Returns(false);
            _selection.Raise(x => x.SelectionChanged += null, (object)null, EventArgs.Empty);
            Assert.True(_context.IsEmpty);
        }
    }
}
