using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Modes;
using Vim.Modes.Visual;
using Vim.UnitTest;

namespace VimCore.Test
{
    [TestFixture]
    public class Visual_DefaultOperationsTests
    {
        private IWpfTextView _textView;
        private MockFactory _factory;
        private Mock<IEditorOperations> _editorOpts;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IStatusUtil> _statusUtil;
        private IOperations _operations;

        private void Create(params string[] lines)
        {
            Create(ModeKind.VisualCharacter, lines);
        }

        private void Create(ModeKind kind, params string[] lines)
        {
            _textView = EditorUtil.CreateView(lines);
            _factory = new MockFactory(MockBehavior.Strict);
            _editorOpts = _factory.Create<IEditorOperations>();
            _jumpList = _factory.Create<IJumpList>();
            _host = _factory.Create<IVimHost>();
            _outlining = _factory.Create<IOutliningManager>();
            _settings = _factory.Create<IVimLocalSettings>();
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();
            _statusUtil = _factory.Create<IStatusUtil>();

            var data = new OperationsData(
                vimHost: _host.Object,
                textView: _textView,
                editorOperations: _editorOpts.Object,
                outliningManager: _outlining.Object,
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                keyMap:null,
                undoRedoOperations: _undoRedoOperations.Object,
                editorOptions:null,
                navigator:null,
                foldManager:null);

            _operations = new DefaultOperations(data, kind);
        }

        private void AssertWorksOnlyOnSingleSpan(Action del)
        {
            Create(ModeKind.VisualLine, "the fox chases the bird");
            _textView.Selection.Mode = TextSelectionMode.Box;
            _statusUtil.Setup(x => x.OnError(Resources.VisualMode_BoxSelectionNotSupported));
            del();
            _factory.Verify();
        }

        [Test]
        public void DeleteSelection1()
        {
            Create("foo", "bar");
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0, 2),false);
            var reg = new Register('c');
            _operations.DeleteSelection(reg);
            Assert.AreEqual("fo", reg.StringValue);
            Assert.AreEqual("o", _textView.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _factory.Verify();
        }

        [Test]
        public void DeleteSelection2()
        {
            Create(ModeKind.VisualLine, "a", "b", "c");
            _textView.Selection.Select(_textView.GetLine(0).ExtentIncludingLineBreak, false);
            var reg = new Register('c');
            _operations.DeleteSelection(reg);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteSelectedLines1()
        {
            Create("foo", "bar");
            var span = _textView.GetLine(0).ExtentIncludingLineBreak;
            _textView.Selection.Select(span, false);
            var reg = new Register('c');
            _operations.DeleteSelectedLines(reg);
            Assert.AreEqual( span.GetText(), reg.StringValue);
            Assert.AreEqual(1, _textView.TextSnapshot.LineCount);
            _factory.Verify();
        }

        [Test]
        public void PasteOverSelection1()
        {
            AssertWorksOnlyOnSingleSpan(() => _operations.PasteOverSelection("foo", new Register('c')));
        }

        [Test]
        public void PasteOverSelection2()
        {
            Create("foo bar ");
            _textView.Selection.Select(new SnapshotSpan(_textView.TextSnapshot, 0, 3), false);
            var reg = new Register('c');
            _operations.PasteOverSelection("again", reg);
            Assert.AreEqual("again bar ", _textView.TextSnapshot.GetText());
            Assert.AreEqual("foo", reg.StringValue);
        }

        [Test]
        [Description("Don't delete the newline on the last line of the selection")]
        public void PasteOverSelection3()
        {
            Create(ModeKind.VisualLine, "a", "b", "c");
            _textView.Selection.Select(_textView.GetLine(0).ExtentIncludingLineBreak, false);
            var reg = new Register('c');
            _operations.PasteOverSelection("hey",reg);
            Assert.AreEqual("hey", _textView.GetLine(0).GetText());
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }
    }
}
