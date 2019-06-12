using System;

namespace Vim.VisualStudio.Implementation.InlineRename
{
    internal interface IInlineRenameUtil
    {
        bool IsRenameActive { get; }

        event EventHandler IsRenameActiveChanged;

        void Cancel();
    }
}
