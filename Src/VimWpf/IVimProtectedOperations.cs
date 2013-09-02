using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EditorUtils;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// This interface is identical to IProtectedOperations except that it can be 
    /// used in an [Import] block.  The IProtectedOperations interface was not reused
    /// for this purpose directly to prevent the chance of another component doing 
    /// the same trick
    /// </summary>
    public interface IVimProtectedOperations : IProtectedOperations
    {

    }
}
