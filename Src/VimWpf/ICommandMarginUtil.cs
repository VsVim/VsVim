using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.UI.Wpf
{
    public interface ICommandMarginUtil
    {
        void SetMarginVisibility(IVimBuffer vimBuffer, bool commandMarginVisible);

        EditableCommand GetStatus(IVimBuffer vimBuffer);
    }
}
