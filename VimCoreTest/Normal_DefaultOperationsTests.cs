using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Normal;
using VimCoreTest.Utils;
using Moq;
using Vim;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text;
using System.Windows.Input;

namespace VimCoreTest
{
    [TestFixture]
    public class Normal_DefaultOperationsTests
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _view;

        private void Create(params string[] lines)
        {
            Create(null, lines);
        }

        private void Create(IEditorOperations editorOpts, params string[] lines)
        {
            if (editorOpts == null)
            {
                var tuple = EditorUtil.CreateViewAndOperations(lines);
                _operationsRaw = new DefaultOperations(tuple.Item1, tuple.Item2);
            }
            else
            {
                var view = EditorUtil.CreateView(lines);
                _operationsRaw = new DefaultOperations(view, editorOpts);
            }

            _operations = _operationsRaw;
            _view = _operations.TextView;
        }

        [Test]
        public void DeleteCharacterAtCursor1()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
        }

        [Test]
        public void DeleteCharacterAtCursor2()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }

        [Test]
        public void DeleteCharacterAtCursor3()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.DeleteCharacterAtCursor(2, reg);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", reg.StringValue);
            Assert.AreEqual(OperationKind.CharacterWise, reg.Value.OperationKind);
        }
        [Test]
        public void DeleteCharacterBeforeCursor1()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            var reg = new Register('c');
            _operations.DeleteCharacterBeforeCursor(1, reg);
            Assert.AreEqual("oo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("f", reg.StringValue);
        }

        [Test, Description("Don't delete past the current line")]
        public void DeleteCharacterBeforeCursor2()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(1).Start);
            _operations.DeleteCharacterBeforeCursor(1, new Register('c'));
            Assert.AreEqual("bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void DeleteCharacterBeforeCursor3()
        {
            Create("foo", "bar");
            _view.Caret.MoveTo(_view.TextSnapshot.GetLineFromLineNumber(0).Start.Add(2));
            var reg = new Register('c');
            _operations.DeleteCharacterBeforeCursor(2, reg);
            Assert.AreEqual("o", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("fo", reg.StringValue);
        }

        [Test]
        public void DeleteCharacterBeforeCursor4()
        {
            Create("foo");
            _operations.DeleteCharacterBeforeCursor(2, new Register('c'));
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }
        [Test]
        public void ReplaceChar1()
        {
            Create("foo");
            _operations.ReplaceChar(InputUtil.CharToKeyInput('b'), 1);
            Assert.AreEqual("boo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar2()
        {
            Create("foo");
            _operations.ReplaceChar(InputUtil.CharToKeyInput('b'), 2);
            Assert.AreEqual("bbo", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test]
        public void ReplaceChar3()
        {
            Create("foo");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            _operations.ReplaceChar(InputUtil.KeyToKeyInput(Key.LineFeed), 1);
            var tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("o", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar4()
        {
            Create("food");
            _view.Caret.MoveTo(new SnapshotPoint(_view.TextSnapshot, 1));
            Assert.IsTrue(_operations.ReplaceChar(InputUtil.KeyToKeyInput(Key.Enter), 2));
            var tss = _view.TextSnapshot;
            Assert.AreEqual(2, tss.LineCount);
            Assert.AreEqual("f", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("d", tss.GetLineFromLineNumber(1).GetText());
        }

        [Test]
        public void ReplaceChar5()
        {
            Create("food");
            var tss = _view.TextSnapshot;
            Assert.IsFalse(_operations.ReplaceChar(InputUtil.CharToKeyInput('c'), 200));
            Assert.AreSame(tss, _view.TextSnapshot);
        }

        [Test, Description("Edit should not cause the cursor to move")]
        public void ReplaceChar6()
        {
            Create("foo");
            Assert.IsTrue(_operations.ReplaceChar(InputUtil.CharToKeyInput('u'), 1));
            Assert.AreEqual(0, _view.Caret.Position.BufferPosition.Position);
        }

        [Test]
        public void YankLines1()
        {
            Create("foo", "bar");
            var reg = new Register('c');
            _operations.YankLines(1, reg);
            Assert.AreEqual("foo" + Environment.NewLine, reg.StringValue);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        [Test]
        public void YankLines2()
        {
            Create("foo", "bar", "jazz");
            var reg = new Register('c');
            _operations.YankLines(2, reg);
            var tss = _view.TextSnapshot;
            var span = new SnapshotSpan(
                tss.GetLineFromLineNumber(0).Start,
                tss.GetLineFromLineNumber(1).EndIncludingLineBreak);
            Assert.AreEqual(span.GetText(), reg.StringValue);
            Assert.AreEqual(OperationKind.LineWise, reg.Value.OperationKind);
        }

        verify paste after and before
    }
}
