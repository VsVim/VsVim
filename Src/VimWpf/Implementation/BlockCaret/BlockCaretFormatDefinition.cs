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
        internal const string Name = VimWpfConstants.BlockCaretFormatDefinitionName;

        internal BlockCaretFormatDefinition()
        {
            DisplayName = "VsVim Block Caret";
            ForegroundColor = Colors.Black;
            BackgroundCustomizable = false;
        }
    }
}
