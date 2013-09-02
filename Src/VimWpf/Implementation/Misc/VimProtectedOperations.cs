using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;

namespace Vim.UI.Wpf.Implementation.Misc
{
    /// <summary>
    /// Simple wrapper around the EditorUtils IProtectedOperations interface.  This provides a handy
    /// export so that the rest of the code can simply import it 
    /// </summary>
    [Export(typeof(IVimProtectedOperations))]
    internal sealed class VimProtectedOperations : IVimProtectedOperations
    {
        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal VimProtectedOperations([ImportMany] IEnumerable<Lazy<IExtensionErrorHandler>> errorHandlers)
        {
            _protectedOperations = EditorUtilsFactory.CreateProtectedOperations(errorHandlers);
        }

        #region IProtectedOperations

        void IProtectedOperations.BeginInvoke(Action action, DispatcherPriority dispatcherPriority)
        {
            _protectedOperations.BeginInvoke(action, dispatcherPriority);
        }

        void IProtectedOperations.BeginInvoke(Action action)
        {
            _protectedOperations.BeginInvoke(action);
        }

        Action IProtectedOperations.GetProtectedAction(Action action)
        {
            return _protectedOperations.GetProtectedAction(action);
        }

        EventHandler IProtectedOperations.GetProtectedEventHandler(EventHandler eventHandler)
        {
            return _protectedOperations.GetProtectedEventHandler(eventHandler);
        }

        void IProtectedOperations.Report(Exception ex)
        {
            _protectedOperations.Report(ex);
        }

        #endregion
    }
}
