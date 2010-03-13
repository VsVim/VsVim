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
    internal static class EditorFormatDefinitionNames
    {
        /// <summary>
        /// When updating this value make sure you also change the Vim.Core tagger
        /// </summary>
        internal const string IncrementalSearch = "vsvim_incrementalsearch";

        /// <summary>
        /// Color of the block caret
        /// </summary>
        internal const string BlockCaret = "vsvim_blockcaret";
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(EditorFormatDefinitionNames.IncrementalSearch)]
    [UserVisible(true)]
    internal sealed class IncrementalSearchMarkerDefinition : MarkerFormatDefinition
    {
        internal IncrementalSearchMarkerDefinition()
        {
            this.DisplayName = "VsVim Incremental Search";
            this.Fill = new SolidColorBrush(Colors.Blue);
        }
    }


    [Export(typeof(EditorFormatDefinition))]
    [Name(EditorFormatDefinitionNames.BlockCaret)]
    [UserVisible(true)]
    internal sealed class BlockCaretMarkerDefinition : EditorFormatDefinition
    {
        internal BlockCaretMarkerDefinition()
        {
            this.DisplayName = "VsVim Block Caret";
            this.ForegroundColor = Colors.Black;
        }
    }


}
