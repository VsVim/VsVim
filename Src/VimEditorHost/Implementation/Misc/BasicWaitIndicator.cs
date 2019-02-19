using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Primitives;
using Microsoft.VisualStudio.Language.Intellisense.Utilities;
using System.Threading;

namespace Vim.EditorHost.Implementation.Misc
{
    [Export(typeof(IWaitIndicator))]
    internal sealed class BasicWaitIndicator : IWaitIndicator
    {
        internal static readonly BasicWaitIndicator Default = new BasicWaitIndicator();

        private readonly IWaitContext _waitContext;

        [ImportingConstructor]
        internal BasicWaitIndicator()
            : this(new UncancellableWaitContext())
        {
        }

        internal BasicWaitIndicator(IWaitContext waitContext)
        {
            _waitContext = waitContext;
        }

        IWaitContext IWaitIndicator.StartWait(string title, string message, bool allowCancel)
        {
            return _waitContext;
        }

        WaitIndicatorResult IWaitIndicator.Wait(string title, string message, bool allowCancel, Action<IWaitContext> action)
        {
            try
            {
                action(_waitContext);
            }
            catch (OperationCanceledException)
            {
                return WaitIndicatorResult.Canceled;
            }

            return WaitIndicatorResult.Completed;
        }

        private sealed class UncancellableWaitContext : IWaitContext
        {
            public CancellationToken CancellationToken
            {
                get { return CancellationToken.None; }
            }

            public void UpdateProgress()
            {
            }

            public bool AllowCancel
            {
                get { return false; }
                set { }
            }

            public string Message
            {
                get { return ""; }
                set { }
            }

            public void Dispose()
            {
            }
        }
    }
}