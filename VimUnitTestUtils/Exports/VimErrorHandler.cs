using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest.Exports
{
    /// <summary>
    /// IVimErrorDetector MEF component.  Useful in tracking down errors which are silently
    /// swallowed by the editor infrastructure
    /// </summary>
    [Export(typeof(IExtensionErrorHandler))]
    [Export(typeof(IVimErrorDetector))]
    public sealed class VimErrorDetector : IExtensionErrorHandler, IVimErrorDetector
    {
        private readonly List<Exception> _errorList = new List<Exception>();

        internal VimErrorDetector()
        {

        }

        void IExtensionErrorHandler.HandleError(object sender, Exception exception)
        {
            _errorList.Add(exception);
        }

        bool IVimErrorDetector.HasErrors()
        {
            return _errorList.Count > 0;
        }

        IEnumerable<Exception> IVimErrorDetector.GetErrors()
        {
            return _errorList;
        }

        void IVimErrorDetector.Clear()
        {
            _errorList.Clear();
        }
    }
}
