using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    public enum CaretDisplay
    {
        Block,
        HalfBlock,
        QuarterBlock,
        Invisible,
        NormalCaret
    }

    public interface IBlockCaret
    {
        ITextView TextView { get; }
        CaretDisplay CaretDisplay { get; set; }
        void Destroy();
    }
}
