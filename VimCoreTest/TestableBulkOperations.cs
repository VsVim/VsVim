using System;

namespace Vim.UnitTest
{
    public sealed class TestableBulkOperations : IBulkOperations, IDisposable
    {
        public int BeginCount { get; set; }
        public int EndCount { get; set; }

        public IDisposable BeginBulkOperation()
        {
            BeginCount++;
            return this;
        }

        public void Dispose()
        {
            EndCount++;
        }

        public bool InBulkOperation
        {
            get { return BeginCount > EndCount; }
        }
    }
}
