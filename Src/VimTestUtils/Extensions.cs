using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Vim.UnitTest
{
    public static class Extensions
    {
        #region Semaphore

        internal static SemaphoreDisposer DisposableWait(this Semaphore semaphore, CancellationToken cancellationToken)
        {
            if (cancellationToken.CanBeCanceled)
            {
                var signalledIndex = WaitHandle.WaitAny(new[] { semaphore, cancellationToken.WaitHandle });
                if (signalledIndex != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw new Exception("Unreacheable");
                }
            }
            else
            {
                semaphore.WaitOne();
            }

            return new SemaphoreDisposer(semaphore);
        }

        internal static Task<SemaphoreDisposer> DisposableWaitAsync(this Semaphore semaphore, CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () => DisposableWait(semaphore, cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        internal readonly struct SemaphoreDisposer : IDisposable
        {
            private readonly Semaphore _semaphore;

            public SemaphoreDisposer(Semaphore semaphore)
            {
                _semaphore = semaphore;
            }

            public void Dispose()
            {
                _semaphore.Release();
            }
        }

        #endregion
    }
}
