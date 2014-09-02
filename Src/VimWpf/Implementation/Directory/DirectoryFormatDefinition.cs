using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    internal sealed class DirectoryFormatDefinition : ClassificationFormatDefinition
    {
        internal const string Name = "vsvim_directory";

        internal DirectoryFormatDefinition()
        {
            DisplayName = "VsVim Directory";
            ForegroundColor = Colors.Green;
        }
    }

    internal sealed class DirectoryClassificationTypes
    {
        [Name(DirectoryFormatDefinition.Name)]
        [Export]
        internal ClassificationTypeDefinition DirectoryClassificationType { get; set; }
    }
}
