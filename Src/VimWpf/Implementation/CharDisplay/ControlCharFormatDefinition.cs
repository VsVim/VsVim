using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.CharDisplay
{
    [Export(typeof(EditorFormatDefinition))]
    [Name(ControlCharFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class ControlCharFormatDefinition : EditorFormatDefinition
    {
        internal const string Name = VimWpfConstants.ControlCharactersFormatDefinitionName;

        internal ControlCharFormatDefinition()
        {
            DisplayName = "VsVim Control Characters";
            ForegroundColor = Colors.Blue;
            BackgroundCustomizable = false;
        }
    }
}
