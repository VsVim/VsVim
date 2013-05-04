using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(BlockCaretFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class BlockCaretFormatDefinition : EditorFormatDefinition
    {
        /// <summary>
        /// Color of the block caret
        /// </summary>
        internal const string Name = "vsvim_blockcaret";

        internal BlockCaretFormatDefinition()
        {
            this.DisplayName = "VsVim Block Caret";
            this.ForegroundColor = Colors.Black;
        }
    }
}
