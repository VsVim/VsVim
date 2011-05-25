using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    /// <summary>
    /// Several portions of the Editor code will swallow exceptions from an extension 
    /// in order to maintain stability.  This is great for a running product like Visual 
    /// Studio but causes tests to silently and incorrectly pass in unit tests.  This component
    /// is useful for helping track down those errors by saving them for later inspection
    /// </summary>
    public interface IVimErrorDetector : IExtensionErrorHandler
    {
        /// <summary>
        /// Are there any recorded errors
        /// </summary>
        bool HasErrors();

        /// <summary>
        /// Get the set of errors which were recorded
        /// </summary>
        /// <returns></returns>
        IEnumerable<Exception> GetErrors();

        /// <summary>
        /// Clear any recorded errors away
        /// </summary>
        void Clear();
    }
}
