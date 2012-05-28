using System;
using System.Collections.Generic;
using Moq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class BulkOperationsTest
    {
        private readonly Mock<IVimHost> _vimHost;
        private readonly BulkOperations _bulkOperationsRaw;
        private readonly IBulkOperations _bulkOperations;

        public BulkOperationsTest()
        {
            _vimHost = new Mock<IVimHost>(MockBehavior.Loose);
            _bulkOperationsRaw = new BulkOperations(_vimHost.Object);
            _bulkOperations = _bulkOperationsRaw;
        }

        /// <summary>
        /// Make sure a single Begin sets the In value
        /// </summary>
        [Fact]
        public void BeginBulkOperations_ShouldSetIn()
        {
            using (_bulkOperations.BeginBulkOperation())
            {
                Assert.True(_bulkOperations.InBulkOperation);
            }
            Assert.False(_bulkOperations.InBulkOperation);
        }

        /// <summary>
        /// Ensure that nested calls to Begin function properly
        /// </summary>
        [Fact]
        public void BeginBulkOperations_Nested()
        {
            var stack = new Stack<IDisposable>();
            for (var i = 0; i < 10; i++)
            {
                stack.Push(_bulkOperations.BeginBulkOperation());
                Assert.True(_bulkOperations.InBulkOperation);
            }

            while (stack.Count > 0)
            {
                stack.Pop().Dispose();
            }

            Assert.False(_bulkOperations.InBulkOperation);
        }

        /// <summary>
        /// Make sure the begin / end methods are called on the host
        /// </summary>
        [Fact]
        public void BeginBulkOperations_CallHostOperations()
        {
            var beginCount = 0;
            var endCount = 0;
            _vimHost.Setup(x => x.BeginBulkOperation()).Callback(() => beginCount++);
            _vimHost.Setup(x => x.EndBulkOperation()).Callback(() => endCount++);
            using (_bulkOperations.BeginBulkOperation())
            {
                Assert.Equal(1, beginCount);
                Assert.Equal(0, endCount);
            }

            Assert.Equal(1, beginCount);
            Assert.Equal(1, endCount);
        }

        /// <summary>
        /// Make sure the begin / end methods are called on the host once for nested
        /// bulk operations
        /// </summary>
        [Fact]
        public void BeginBulkOperations_CallHostOperationsNested()
        {
            var beginCount = 0;
            var endCount = 0;
            _vimHost.Setup(x => x.BeginBulkOperation()).Callback(() => beginCount++);
            _vimHost.Setup(x => x.EndBulkOperation()).Callback(() => endCount++);

            var stack = new Stack<IDisposable>();
            for (var i = 0; i < 10; i++)
            {
                stack.Push(_bulkOperations.BeginBulkOperation());
                Assert.Equal(1, beginCount);
                Assert.Equal(0, endCount);
            }

            while (stack.Count > 0)
            {
                stack.Pop().Dispose();
            }

            Assert.Equal(1, beginCount);
            Assert.Equal(1, endCount);
        }
    }
}
