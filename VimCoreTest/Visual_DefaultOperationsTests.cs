using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim.Modes.Visual;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;
using Microsoft.VisualStudio.Text.Operations;
using Vim;

namespace VimCoreTest
{
    [TestFixture]
    public class Visual_DefaultOperationsTests
    {
        private IWpfTextView _view;
        private Mock<IEditorOperations> _editorOpts;
        private Mock<ISelectionTracker> _tracker;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private IOperations _operations;

        private void Create(params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _editorOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _tracker = new Mock<ISelectionTracker>(MockBehavior.Strict);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _operations = new DefaultOperations(_view, _editorOpts.Object, _host.Object, _jumpList.Object, _tracker.Object);
        }

        [Test]
        public void DeleteSelection1()
        {
            Create("foo", "bar");
            _view.Selection.Select(new SnapshotSpan(_view.TextSnapshot, 0, 2),false);
            _tracker.SetupGet(x => x.SelectedText).Returns("fo").Verifiable();
            var reg = new Register('c');
            _operations.DeleteSelection(reg);
            Assert.AreEqual("fo", reg.StringValue);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _tracker.Verify();
        }

        [Test]
        public void DeleteSelectedLines1()
        {
            Create("foo", "bar");
            var span = _view.GetLineSpan(0, 0);
            _tracker.SetupGet(x => x.SelectedLines).Returns(span).Verifiable();
            var reg = new Register('c');
            _operations.DeleteSelectedLines(reg);
            Assert.AreEqual(span.GetText(), reg.StringValue);
            Assert.AreEqual(1, _view.TextSnapshot.LineCount);
            _tracker.Verify();
        }
    }
}
