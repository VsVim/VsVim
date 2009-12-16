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
    }
}
