using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class BulkOperationsTest
    {
        private Mock<IVimHost> _vimHost;
        private BulkOperations _bulkOperationsRaw;
        private IBulkOperations _bulkOperations;

        [SetUp]
        public void Setup()
        {
            _vimHost = new Mock<IVimHost>(MockBehavior.Loose);
            _bulkOperationsRaw = new BulkOperations(_vimHost.Object);
            _bulkOperations = _bulkOperationsRaw;
        }

        /// <summary>
        /// Make sure a single Begin sets the In value
        /// </summary>
        [Test]
        public void BeginBulkOperations_ShouldSetIn()
        {
            using (_bulkOperations.BeginBulkOperation())
            {
                Assert.IsTrue(_bulkOperations.InBulkOperation);
            }
            Assert.IsFalse(_bulkOperations.InBulkOperation);
        }

        /// <summary>
        /// Ensure that nested calls to Begin function properly
        /// </summary>
        [Test]
        public void BeginBulkOperations_Nested()
        {
            var stack = new Stack<IDisposable>();
            for (var i = 0; i < 10; i++)
            {
                stack.Push(_bulkOperations.BeginBulkOperation());
                Assert.IsTrue(_bulkOperations.InBulkOperation);
            }

            while (stack.Count > 0)
            {
                stack.Pop().Dispose();
            }

            Assert.IsFalse(_bulkOperations.InBulkOperation);
        }

        /// <summary>
        /// Make sure the begin / end methods are called on the host
        /// </summary>
        [Test]
        public void BeginBulkOperations_CallHostOperations()
        {
            var beginCount = 0;
            var endCount = 0;
            _vimHost.Setup(x => x.BeginBulkOperation()).Callback(() => beginCount++);
            _vimHost.Setup(x => x.EndBulkOperation()).Callback(() => endCount++);
            using (_bulkOperations.BeginBulkOperation())
            {
                Assert.AreEqual(1, beginCount);
                Assert.AreEqual(0, endCount);
            }

            Assert.AreEqual(1, beginCount);
            Assert.AreEqual(1, endCount);
        }

        /// <summary>
        /// Make sure the begin / end methods are called on the host once for nested
        /// bulk operations
        /// </summary>
        [Test]
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
                Assert.AreEqual(1, beginCount);
                Assert.AreEqual(0, endCount);
            }

            while (stack.Count > 0)
            {
                stack.Pop().Dispose();
            }

            Assert.AreEqual(1, beginCount);
            Assert.AreEqual(1, endCount);
        }
    }
}
