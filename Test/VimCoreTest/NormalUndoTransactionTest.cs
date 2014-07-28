using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Operations;
using Moq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public class NormalUndoTransactionTest : VimTestBase
    {
        private MockRepository _factory;
        private Mock<ITextUndoTransaction> _realTransaction;
        private NormalUndoTransaction _transactionRaw;
        private IUndoTransaction _transaction;

        public void Create(bool haveRealTransaction = true)
        {
            _factory = new MockRepository(MockBehavior.Strict);

            var undoRedoOperations = new UndoRedoOperations(new StatusUtil(), FSharpOption<ITextUndoHistory>.None, EditorOperationsFactoryService); 
            if (haveRealTransaction)
            {
                _realTransaction = _factory.Create<ITextUndoTransaction>();
                _transactionRaw = new NormalUndoTransaction("Undo", FSharpOption.Create(_realTransaction.Object), undoRedoOperations);
            }
            else
            {
                _transactionRaw = new NormalUndoTransaction("Undo", FSharpOption<ITextUndoTransaction>.None, undoRedoOperations);
            }
            _transaction = _transactionRaw;
        }

        [Fact]
        public void Complete1()
        {
            Create(haveRealTransaction: false);
            _transaction.Complete();
        }

        [Fact]
        public void Complete2()
        {
            Create();
            _realTransaction.Setup(x => x.Complete()).Verifiable();
            _transaction.Complete();
            _factory.Verify();
        }

        [Fact]
        public void Cancel1()
        {
            Create(haveRealTransaction: false);
            _transaction.Cancel();
        }

        [Fact]
        public void Cancel2()
        {
            Create();
            _realTransaction.Setup(x => x.Cancel()).Verifiable();
            _transaction.Cancel();
            _factory.Verify();
        }

    }
}
