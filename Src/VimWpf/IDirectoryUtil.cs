using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// This is a MEF importable interface which controls directory operations
    /// </summary>
    public interface IDirectoryUtil
    {
        /// <summary>
        /// If this is a directory buffer then it returns the path of the directory it was created 
        /// for.  Else it returns null
        /// </summary>
        string GetDirectoryPath(ITextBuffer textBuffer);

        /// <summary>
        /// Create an ITextBuffer instance which represents the specified directory.  If the directory
        /// cannot be read then this will fail 
        /// </summary>
        bool TryCreateDirectoryTextBuffer(string directoryPath, out ITextBuffer textBuffer);
    }
}
