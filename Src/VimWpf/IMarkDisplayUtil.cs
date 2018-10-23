using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Controls the display of marks in the editor margin.
    /// </summary>
    public interface IMarkDisplayUtil
    {
        /// <summary>
        /// The char representation of marks which should be hidden in the margin.
        /// </summary>
        string HideMarks { get; set; }

        /// <summary>
        /// Raised when <see cref="HideMarks"/> changes.
        /// </summary>
        event EventHandler HideMarksChanged;
    }
}
