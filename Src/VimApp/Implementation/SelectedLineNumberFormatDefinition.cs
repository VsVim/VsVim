using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace VimApp.Implementation
{
    // VimApp does not have a Selected Line Number FormatDefinition
    // so we add it here
    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = Name)]
    [Name(Name)]
    internal class SelectedLineNumberFormatDefinition : ClassificationFormatDefinition
    {
        internal const string Name = "Selected Line Number";

        internal SelectedLineNumberFormatDefinition()
        {
            DisplayName = Name;
            ForegroundColor = Colors.Red;
        }
    }

    internal sealed class SelectedLineNumberClassificationType
    {
        [Name(SelectedLineNumberFormatDefinition.Name)]
        [Export]
        internal ClassificationTypeDefinition SelectedLineNumberTypeDefinition { get; set; }
    }
}
