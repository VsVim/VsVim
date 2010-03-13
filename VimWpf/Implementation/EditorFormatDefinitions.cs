using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Classification;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;

namespace Vim.UI.Wpf.Implementation
{
    [Export(typeof(EditorFormatDefinition))]
    [Name("vsvim_incrementalsearch")]
    [UserVisible(true)]
    internal sealed class IncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal IncrementalSearchMarkerDefinition()
        {
            this.DisplayName = "VsVim Incremental Search";
            this.Fill = new SolidColorBrush(Colors.Blue);
        }
    }
}
