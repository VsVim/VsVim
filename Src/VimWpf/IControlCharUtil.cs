using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// VsVim like gVim alters the display of control characters in the the editor.  Due to some 
    /// constraints of the editor it doesn't change the display of all control characters, just a 
    /// well defined subset.  This interface abstracts set out
    /// </summary>
    public interface IControlCharUtil
    {
        /// <summary>
        /// Is this a character which has its display special cased 
        /// </summary>
        bool IsDisplayControlChar(char c);

        /// <summary>
        /// Try and get the textual representation for the specified char
        /// </summary>
        bool TryGetDisplayText(char c, out string text);
    }
}
