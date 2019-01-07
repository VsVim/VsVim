using System;
using System.Threading;
using System.Threading.Tasks;

namespace Vim.UI.Wpf.RelativeLineNumbers.Util
{
    public class SafeRefreshLock
    {
        private int _inProgress;

        private int _refreshRequested;

        public async Task ExecuteInLockAsync(Func<Task> operation)
        {
            if (!TryObtainRedrawLock())
            {
                return;
            }

            await operation().ConfigureAwait(false);

            if (!TryReleaseRedrawLock())
            {
                await ExecuteInLockAsync(operation).ConfigureAwait(false);
            }
        }

        private static bool TryExchange(ref int location, int value, int comparand)
        {
            int previousValue =
                Interlocked.CompareExchange(ref location, value, comparand);

            return previousValue == comparand;
        }

        private bool TryObtainRedrawLock()
        {
            bool canAcquireLock = TryExchange(ref _inProgress, 1, 0);

            if (!canAcquireLock)
            {
                canAcquireLock = TryExchange(ref _refreshRequested, 1, 0);
                canAcquireLock = canAcquireLock && TryExchange(ref _inProgress, 1, 0);
                canAcquireLock = canAcquireLock && TryExchange(ref _refreshRequested, 0, 1);
            }

            if (canAcquireLock)
            {
                Interlocked.Exchange(ref _refreshRequested, 0);
            }

            return canAcquireLock;
        }

        private bool TryReleaseRedrawLock()
        {
            bool wasNewUpdateRequested = TryExchange(ref _refreshRequested, 1, 0);

            Interlocked.Exchange(ref _inProgress, 0);

            return wasNewUpdateRequested;
        }
    }
}