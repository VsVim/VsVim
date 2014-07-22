using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.VisualStudio.Implementation
{
    internal static class EditorFormatDefinitionNames
    {
        internal const string Margin = Constants.ToastMarginFormatDefinitionName;
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(EditorFormatDefinitionNames.Margin)]
    [UserVisible(true)]
    internal sealed class MarginFormatDefinition : EditorFormatDefinition
    {
        internal static Color DefaultColor = Colors.Wheat;

        internal MarginFormatDefinition()
        {
            DisplayName = "VsVim Margins";
            BackgroundColor = DefaultColor;
        }
    }
}
