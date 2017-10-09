#light

namespace Vim
open System
open System.Windows.Threading

/// Alternate method of dispatching calls.  This wraps the Dispatcher type and will
/// gracefully handle dispatch errors.  Without this layer exceptions coming from a 
/// dispatched operation will go directly to the dispatch loop and crash the host
/// application
type IProtectedOperations = 

    /// Get an Action delegate which invokes the original action and handles any
    /// thrown Exceptions by passing them off the the available IExtensionErrorHandler
    /// values
    abstract member GetProtectedAction : action : Action -> Action

    /// Get an EventHandler delegate which invokes the original action and handles any
    /// thrown Exceptions by passing them off the the available IExtensionErrorHandler
    /// values
    abstract member GetProtectedEventHandler : eventHandler : EventHandler -> EventHandler

    /// Dispatch the given delegate for action.  If it fails the editor error
    /// handling system will be notified
    abstract member BeginInvoke : Action -> unit

    /// Dispatch the given delegate for action.  If it fails the editor error
    /// handling system will be notified
    abstract member BeginInvoke : Action * DispatcherPriority -> unit

    /// Report an Exception to the IExtensionErrorHandlers
    abstract member Report : ex : Exception -> unit
