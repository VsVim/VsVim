using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;

namespace Vim.UI.Wpf.Implementation
{
    /// <summary>
    /// Implements the safe dispatching interface which prevents application crashes for 
    /// exceptions reaching the dispatcher loop
    /// </summary>
    [Export(typeof(IProtectedOperations))]
    internal sealed class ProtectedOperations : IProtectedOperations
    {
        private readonly List<Lazy<IExtensionErrorHandler>> _errorHandlers;

        [ImportingConstructor]
        internal ProtectedOperations([ImportMany]IEnumerable<Lazy<IExtensionErrorHandler>> errorHandlers)
        {
            _errorHandlers = errorHandlers.ToList();
        }

        internal ProtectedOperations(IExtensionErrorHandler errorHandler)
        {
            var lazy = new Lazy<IExtensionErrorHandler>(() => errorHandler);
            _errorHandlers = new List<Lazy<IExtensionErrorHandler>>(new [] { lazy });
        }

        /// <summary>
        /// Create a SafeDispatcher instance which doesn't have any backing IExtensionErrorHandler
        /// values.  Useful for test scenarios
        /// </summary>
        internal ProtectedOperations()
        {

        }

        /// <summary>
        /// Produce a delegate that can safely execute the given action.  If it throws an exception 
        /// then make sure to alert the error handlers
        /// </summary>
        private Action GetProtectedAction(Action action)
        {
            Action protectedAction =
                () =>
                {

                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        AlertAll(e);
                    }
                };
            return protectedAction;
        }

        private EventHandler GetProtectedEventHandler(EventHandler eventHandler)
        {
            EventHandler protectedEventHandler =
                (sender, e) =>
                {
                    try
                    {
                        eventHandler(sender, e);
                    }
                    catch (Exception exception)
                    {
                        AlertAll(exception);
                    }
                };

            return protectedEventHandler;
        }

        /// <summary>
        /// Alert all of the IExtensionErrorHandlers that the given Exception occurred.  Be careful to guard
        /// against them for Exceptions as we are still on the dispatcher loop here and exceptions would be
        /// fatal
        /// </summary>
        private void AlertAll(Exception originalException)
        {
            foreach (var errorHandler in _errorHandlers)
            {
                try
                {
                    errorHandler.Value.HandleError(this, originalException);
                }
                catch (Exception exception)
                {
                    Debug.Fail("Error Handler Threw: " + exception.Message);
                }
            }
        }


        void IProtectedOperations.BeginInvoke(Action action)
        {
            var protectedAction = GetProtectedAction(action);
            Dispatcher.CurrentDispatcher.BeginInvoke(protectedAction, null);
        }

        void IProtectedOperations.BeginInvoke(Action action, DispatcherPriority dispatcherPriority)
        {
            var protectedAction = GetProtectedAction(action);
            Dispatcher.CurrentDispatcher.BeginInvoke(protectedAction, dispatcherPriority);
        }

        Action IProtectedOperations.GetProtectedAction(Action action)
        {
            return GetProtectedAction(action);
        }

        EventHandler IProtectedOperations.GetProtectedEventHandler(EventHandler eventHandler)
        {
            return GetProtectedEventHandler(eventHandler);
        }
    }
}
