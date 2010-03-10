using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    public interface IBlockCaret
    {
        ITextView TextView { get; }
        bool IsShown { get; }
        void Show();
        void Hide();
        void Destroy();
    }
}
