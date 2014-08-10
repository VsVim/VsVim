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
    [Name(DirectoryFormatDefinition.Name)]
    [UserVisible(true)]
    internal sealed class DirectoryFormatDefinition : MarkerFormatDefinition
    {
        internal const string Name = VimWpfConstants.DirectoryFormatDefinitionName;

        internal DirectoryFormatDefinition()
        {
            DisplayName = "VsVim Directory";
            ForegroundColor = Colors.Green;
        }
    }
}
