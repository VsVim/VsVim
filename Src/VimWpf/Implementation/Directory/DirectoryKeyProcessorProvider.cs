using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(IKeyProcessorProvider))]
    [ContentType(VimWpfConstants.DirectoryContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Directory Key Processor")]
    [Order(Before = VimConstants.MainKeyProcessorName)]
    internal sealed class DirectoryKeyProcessorProvider : IKeyProcessorProvider
    {
        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            return null;
        }
    }
}
