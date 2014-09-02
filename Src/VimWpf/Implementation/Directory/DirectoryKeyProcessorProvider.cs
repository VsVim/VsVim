using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace Vim.UI.Wpf.Implementation.Directory
{
    [Export(typeof(IKeyProcessorProvider))]
    [ContentType(DirectoryContentType.Name)]
    [TextViewRole(PredefinedTextViewRoles.Interactive)]
    [Name("Directory Key Processor")]
    [Order(Before = VimConstants.MainKeyProcessorName)]
    internal sealed class DirectoryKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IDirectoryUtil _directoryUtil;
        private readonly IVimHost _vimHost;

        [ImportingConstructor]
        internal DirectoryKeyProcessorProvider(IDirectoryUtil directoryUtil, IVimHost vimHost)
        {
            _directoryUtil = directoryUtil;
            _vimHost = vimHost;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            var directoryPath = _directoryUtil.GetDirectoryPath(wpfTextView.TextBuffer);
            return new DirectoryKeyProcessor(directoryPath, _vimHost, wpfTextView);
        }
    }
}
