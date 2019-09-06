using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(SecondaryCaretFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class SecondaryCaretFormatDefinition : EditorFormatDefinition
    {
        /// <summary>
        /// Color of a secondary caret
        /// </summary>
        internal const string Name = VimWpfConstants.SecondaryCaretFormatDefinitionName;

        internal SecondaryCaretFormatDefinition()
        {
            DisplayName = "VsVim Secondary Caret";
            ForegroundColor = Colors.White;
            BackgroundColor = Colors.DarkBlue;
        }
    }
}
