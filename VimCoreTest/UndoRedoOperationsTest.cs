using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Vim;
using Microsoft.VisualStudio.Text.Operations;
using Vim.Extensions;
using Microsoft.FSharp.Core;

namespace VimCore.Test
{
    [TestFixture]
    public class UndoRedoOperationsTest
    {
        private MockFactory _factory;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<ITextUndoHistory> _history;
        private UndoRedoOperations _operationsRaw;
        private IUndoRedoOperations _operations;

        public void Create(bool haveHistory = true)
        {
            _factory = new MockFactory(MockBehavior.Strict);
            _statusUtil = _factory.Create<IStatusUtil>();
            if (haveHistory)
            {
                _history = _factory.Create<ITextUndoHistory>();
                _operationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption.Create(_history.Object));
            }
            else
            {
                _operationsRaw = new UndoRedoOperations(
                    _statusUtil.Object,
                    FSharpOption<ITextUndoHistory>.None);
            }
            _operations = _operationsRaw;
        }

        [Test]
        public void Undo1()
        {
            Create(haveHistory: false);
            _statusUtil.Setup(x => x.OnError(Resources.UndoRedo_NotSupported)).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
        }

        [Test]
        public void Undo2()
        {
            Create();
            _history.SetupGet(x => x.CanUndo).Returns(false).Verifiable();
            _operationsRaw.Undo(42);
            _factory.Verify();
        }

        [Test]
        public void Undo3()
        {
            Create();
            _statusUtil.Setup(x => x.OnError(Resources.UndoRedo_CannotUndo)).Verifiable();
            _history.SetupGet(x => x.CanUndo).Returns(true).Verifiable();
            _history.Setup(x => x.Undo(1)).Throws(new NotSupportedException()).Verifiable();
            _operationsRaw.Undo(1);
            _factory.Verify();
        }

        [Test]
        public void Undo4()
        {
            Create();
            _history.SetupGet(x => x.CanUndo).Returns(true).Verifiable();
            _history.Setup(x => x.Undo(2)).Verifiable();
            _operationsRaw.Undo(2);
            _factory.Verify();
        }

        [Test]
        public void Redo1()
        {
            Create(haveHistory: false);
            _statusUtil.Setup(x => x.OnError(Resources.UndoRedo_NotSupported)).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
        }

        [Test]
        public void Redo2()
        {
            Create();
            _history.SetupGet(x => x.CanRedo).Returns(false).Verifiable();
            _operationsRaw.Redo(42);
            _factory.Verify();
        }

        [Test]
        public void Redo3()
        {
            Create();
            _statusUtil.Setup(x => x.OnError(Resources.UndoRedo_CannotRedo)).Verifiable();
            _history.SetupGet(x => x.CanRedo).Returns(true).Verifiable();
            _history.Setup(x => x.Redo(1)).Throws(new NotSupportedException()).Verifiable();
            _operationsRaw.Redo(1);
            _factory.Verify();
        }

        [Test]
        public void Redo4()
        {
            Create();
            _history.SetupGet(x => x.CanRedo).Returns(true).Verifiable();
            _history.Setup(x => x.Redo(2)).Verifiable();
            _operationsRaw.Redo(2);
            _factory.Verify();
        }

        [Test]
        public void CreateUndoTransaction1()
        {
            Create(haveHistory: false);
            var transaction = _operationsRaw.CreateUndoTransaction("foo");
            Assert.IsNotNull(transaction);
            _factory.Verify();
        }

        [Test]
        public void CreateUndoTransaction2()
        {
            Create();
            var mock = _factory.Create<ITextUndoTransaction>();
            _history.Setup(x => x.CreateTransaction("foo")).Returns(mock.Object).Verifiable();
            var transaction = _operationsRaw.CreateUndoTransaction("foo");
            Assert.IsNotNull(transaction);
            _factory.Verify();
        }

    }
}
