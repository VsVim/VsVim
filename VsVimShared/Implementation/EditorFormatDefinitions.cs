using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VsVim.Implementation
{
    internal static class EditorFormatDefinitionNames
    {
        internal const string ConflictingKeyBindingMargin = "vsvim_conflictingkeybindingmargin";
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(EditorFormatDefinitionNames.ConflictingKeyBindingMargin)]
    [UserVisible(true)]
    internal sealed class ConflictingKeyBindingMarginFormatDefinition : EditorFormatDefinition
    {
        internal static Color DefaultColor = Colors.Wheat;

        internal ConflictingKeyBindingMarginFormatDefinition()
        {
            DisplayName = "VsVim Conflicting Key Binding Margin";
            BackgroundColor = DefaultColor;
        }
    }
}
