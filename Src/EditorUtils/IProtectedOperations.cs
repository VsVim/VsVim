using System;
using System.Windows.Threading;

namespace EditorUtils
{
    /// <summary>
    /// Alternate method of dispatching calls.  This wraps the Dispatcher type and will
    /// gracefully handle dispatch errors.  Without this layer exceptions coming from a 
    /// dispatched operation will go directly to the dispatch loop and crash the host
    /// application
    /// </summary>
    public interface IProtectedOperations
    {
        /// <summary>
        /// Get an Action delegate which invokes the original action and handles any
        /// thrown Exceptions by passing them off the the available IExtensionErrorHandler
        /// values
        /// </summary>
        Action GetProtectedAction(Action action);

        /// <summary>
        /// Get an EventHandler delegate which invokes the original action and handles any
        /// thrown Exceptions by passing them off the the available IExtensionErrorHandler
        /// values
        /// </summary>
        EventHandler GetProtectedEventHandler(EventHandler eventHandler);

        /// <summary>
        /// Dispatch the given delegate for action.  If it fails the editor error
        /// handling system will be notified
        /// </summary>
        void BeginInvoke(Action action);

        /// <summary>
        /// Dispatch the given delegate for action.  If it fails the editor error
        /// handling system will be notified
        /// </summary>
        void BeginInvoke(Action action, DispatcherPriority dispatcherPriority);

        /// <summary>
        /// Report an Exception to the IExtensionErrorHandlers
        /// </summary>
        void Report(Exception ex);
    }
}


