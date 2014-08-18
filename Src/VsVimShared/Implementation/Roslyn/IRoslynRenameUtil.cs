using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vim.VisualStudio.Implementation.Roslyn
{
    internal interface IRoslynRenameUtil
    {
        bool IsRenameActive { get; }

        event EventHandler IsRenameActiveChanged;
    }
}
