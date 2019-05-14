﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Vim.UnitTest.Utilities
{
    // Based on CoreCLR's implementation of the TaskScheduler they return from TaskScheduler.FromCurrentSynchronizationContext
    internal class SynchronizationContextTaskScheduler : TaskScheduler
    {
        private readonly SendOrPostCallback _postCallback;
        private readonly DispatcherSynchronizationContext _synchronizationContext;

        internal SynchronizationContextTaskScheduler(DispatcherSynchronizationContext synchronizationContext)
        {
            _postCallback = new SendOrPostCallback(PostCallback);
            _synchronizationContext = synchronizationContext ?? throw new ArgumentNullException(nameof(synchronizationContext));
        }

        public override Int32 MaximumConcurrencyLevel => 1;

        protected override void QueueTask(Task task)
        {
            _synchronizationContext.Post(_postCallback, task);
        }
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (SynchronizationContext.Current == _synchronizationContext)
            {
                return TryExecuteTask(task);
            }

            return false;
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return null;
        }

        private void PostCallback(object obj)
        {
            TryExecuteTask((Task)obj);
        }
    }
}
