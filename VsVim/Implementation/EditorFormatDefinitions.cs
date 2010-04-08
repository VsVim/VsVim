using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Classification;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;

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
            this.DisplayName = "VsVim Conflicting Key Binding Margin";
            this.BackgroundColor = DefaultColor;
        }
    }
}
