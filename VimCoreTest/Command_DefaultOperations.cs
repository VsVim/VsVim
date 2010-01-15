using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using VimCoreTest.Utils;
using Vim.Modes;
using Vim.Modes.Command;
using Moq;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest
{
    [TestFixture]
    public class Command_DefaultOperations
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _view;
        private Mock<IEditorOperations> _editOpts;
        private Mock<IVimHost> _host;

        private void Create(params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _editOpts = new Mock<IEditorOperations>(MockBehavior.Strict);
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            _operationsRaw = new DefaultOperations(_view, _editOpts.Object, _host.Object);
            _operations = _operationsRaw;
        }
        
        [Test]
        public void Put1()
        {
            Create("foo");
            _operations.Put("bar", _view.TextSnapshot.GetLineFromLineNumber(0), false);
        }

        [Test]
        public void Put2()
        {
            Create("bar", "baz");
            _operations.Put(" here", _view.TextSnapshot.GetLineFromLineNumber(0), true);
            var tss = _view.TextSnapshot;
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" here", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual(tss.GetLineFromLineNumber(1).Start.Add(1).Position, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void Substitute1()
        {
            Create("bar", "foo");
            var tss = _view.TextSnapshot;
            _operations.Substitute("bar", "again", new SnapshotSpan(tss, 0, 3), SubstituteFlags.None);
            Assert.AreEqual("again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Only once per line")]
        public void Substitute2()
        {
            Create("bar bar ", "foo");
            var tss = _view.TextSnapshot;
            _operations.Substitute("bar", "again", new SnapshotSpan(tss, 0, tss.Length), SubstituteFlags.None);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Should run on every line in the span")]
        public void Substitute3()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("bar", "again", new SnapshotSpan(tss, 0, tss.Length), SubstituteFlags.None);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo again", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Replace all if the option is set")]
        public void Substitute4()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("bar", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Ignore case")]
        public void Substitute5()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("BAR", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.IgnoreCase);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute6()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("BAR", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute7()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("BAR", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Ignore case and Ordinal. Not defined but Gvim will prefer Ordinal here")]
        public void Substitute8()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            _operations.Substitute("BAR", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.IgnoreCase | SubstituteFlags.OrdinalCase);
            Assert.AreEqual("bar bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("No matches")]
        public void Substitute9()
        {
            Create("bar bar ", "foo bar");
            var tss = _view.TextSnapshot;
            var pattern = "BAR";
            _host.Setup(x => x.UpdateStatus(Resources.CommnadMode_PatternNotFound(pattern))).Verifiable();
            _operations.Substitute("BAR", "again", tss.GetLineFromLineNumber(0).Extent, SubstituteFlags.OrdinalCase);
            _host.Verify();
        }
    }
}
