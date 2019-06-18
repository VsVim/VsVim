using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(PrimaryCaretFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class PrimaryCaretFormatDefinition : EditorFormatDefinition
    {
        /// <summary>
        /// Color of the primary caret
        /// </summary>
        internal const string Name = VimWpfConstants.PrimaryCaretFormatDefinitionName;

        internal PrimaryCaretFormatDefinition()
        {
            DisplayName = "VsVim Primary Caret";
            ForegroundColor = Colors.White;
            BackgroundColor = Colors.DarkRed;
        }
    }
}
