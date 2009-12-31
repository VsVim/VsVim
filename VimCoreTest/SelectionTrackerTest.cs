using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Visual;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    [TestFixture]
    public class SelectionTrackerTest
    {
        private ITextView _view;
        private SelectionTracker _tracker;

        private void Create(SelectionMode mode, params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _tracker = new SelectionTracker(_view, mode);
            _tracker.Start(FSharpOption<SnapshotPoint>.None);
        }

        [Test]
        public void InExplicitMove1()
        {
            Create(SelectionMode.Block, "foo");
            _tracker.BeginExplicitMove();
            Assert.IsTrue(_tracker.InExplicitMove);
        }

        [Test]
        public void InExplicitMove2()
        {
            Create(SelectionMode.Character, "");
            Assert.IsFalse(_tracker.InExplicitMove);
            _tracker.BeginExplicitMove();
            _tracker.BeginExplicitMove();
            _tracker.EndExplicitMove();
            _tracker.EndExplicitMove();
            Assert.IsFalse(_tracker.InExplicitMove);
        }

        [Test]
        public void AnchorPoint1()
        {
            Create(SelectionMode.Character, "foo");
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreEqual(0, _tracker.AnchorPoint.Position);
            Assert.AreSame(_view.TextSnapshot, _tracker.AnchorPoint.Snapshot);
        }

        [Test, Description("Shouldn't track if it's Stopp'd")]
        public void AnchorPoint2()
        {
            Create(SelectionMode.Character, "foo");
            _tracker.Stop();
            _view.TextBuffer.Replace(new Span(0, 1), "h");
            Assert.AreNotSame(_view.TextSnapshot, _tracker.AnchorPoint.Snapshot);
        }
    }
}
