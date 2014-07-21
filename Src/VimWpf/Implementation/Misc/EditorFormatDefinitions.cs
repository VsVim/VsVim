using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.Misc
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.IncrementalSearchTagName)]
    [UserVisible(true)]
    internal sealed class IncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal IncrementalSearchMarkerDefinition()
        {
            DisplayName = "VsVim Incremental Search";
            BackgroundColor = Colors.LightBlue;
            ForegroundCustomizable = false;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(VimConstants.HighlightIncrementalSearchTagName)]
    [UserVisible(true)]
    internal sealed class HighlightIncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal HighlightIncrementalSearchMarkerDefinition()
        {
            DisplayName = "VsVim Highlight Incremental Search";
            BackgroundColor = Colors.LightBlue;
            ForegroundCustomizable = false;
        }
    }
}
