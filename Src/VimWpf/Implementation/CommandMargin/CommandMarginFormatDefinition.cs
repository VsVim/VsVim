using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(CommandMarginFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class CommandMarginFormatDefinition : EditorFormatDefinition
    {
        internal const string Name = "vsvim_commandmargin";

        internal CommandMarginFormatDefinition()
        {
            this.DisplayName = "VsVim Command Window";
            this.ForegroundColor = Colors.Black;
            this.BackgroundColor = Colors.White;
        }
    }
}
