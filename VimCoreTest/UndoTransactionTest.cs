using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Moq;
using Microsoft.VisualStudio.Text.Operations;
using Vim;
using Vim.Extensions;
using Microsoft.FSharp.Core;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class UndoTransactionTest
    {
        private MockRepository _factory;
        private Mock<ITextUndoTransaction> _realTransaction;
        private UndoTransaction _transactionRaw;
        private IUndoTransaction _transaction;

        public void Create(bool haveRealTransaction = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);
            if (haveRealTransaction)
            {
                _realTransaction = _factory.Create<ITextUndoTransaction>();
                _transactionRaw = new UndoTransaction(FSharpOption.Create(_realTransaction.Object));
            }
            else
            {
                _transactionRaw = new UndoTransaction(FSharpOption<ITextUndoTransaction>.None);
            }
            _transaction = _transactionRaw;
        }

        [Test]
        public void Complete1()
        {
            Create(haveRealTransaction: false);
            _transaction.Complete();
        }

        [Test]
        public void Complete2()
        {
            Create();
            _realTransaction.Setup(x => x.Complete()).Verifiable();
            _transaction.Complete();
            _factory.Verify();
        }

        [Test]
        public void Cancel1()
        {
            Create(haveRealTransaction: false);
            _transaction.Cancel();
        }

        [Test]
        public void Cancel2()
        {
            Create();
            _realTransaction.Setup(x => x.Cancel()).Verifiable();
            _transaction.Cancel();
            _factory.Verify();
        }

    }
}
