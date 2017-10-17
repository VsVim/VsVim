using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.Win32;
using Microsoft.VisualStudio.Language.Intellisense.Utilities;
using System.Threading;

namespace EditorUtils
{
    public sealed partial class EditorHostFactory
    {
        /// <summary>
        /// The IWaitIndicator type is a new export in VS2017.  This approach is a temporary work around that 
        /// is not a long term solution.  It will only work when both running against VS2017 and using EditorUtils
        /// compiled for VS2017.  In all other cases it will fail because this export can only be created in 
        /// the versions compiled against VS2017.
        /// </summary>
#if VS2017
        [Export(typeof(IWaitIndicator))]
        public sealed class SimpleWaitIndicator : IWaitIndicator
        {
            public static readonly SimpleWaitIndicator Default = new SimpleWaitIndicator();

            private readonly IWaitContext _waitContext;

            public SimpleWaitIndicator()
                : this(new UncancellableWaitContext())
            {
            }

            internal SimpleWaitIndicator(IWaitContext waitContext)
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
#endif
    }
}
