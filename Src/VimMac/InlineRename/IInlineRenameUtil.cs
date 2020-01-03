using System;

namespace Vim.UI.Cocoa.Implementation.InlineRename
{
    internal interface IInlineRenameUtil
    {
        bool IsRenameActive { get; }

        event EventHandler IsRenameActiveChanged;

        void Cancel();
    }
}
