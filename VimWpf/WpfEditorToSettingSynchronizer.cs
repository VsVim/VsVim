using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// This type is used to synchronize certain Vim settings to the equivalent WPF ones
    /// </summary>
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class WpfEditorToSettingSynchronizer : IVimBufferCreationListener
    {
        private readonly IBlockCaretFactoryService _blockCaretFactoryService;
        private readonly IEditorOptionsFactoryService _editorOptionsFatoryService;

        [ImportingConstructor]
        internal WpfEditorToSettingSynchronizer(
            IBlockCaretFactoryService blockCaretFactoryService,
            IEditorOptionsFactoryService editorOptionsFactoryService)
        {
            _blockCaretFactoryService = blockCaretFactoryService;
            _editorOptionsFatoryService = editorOptionsFactoryService;
        }

        public void VimBufferCreated(IVimBuffer buffer)
        {
            var textView = buffer.TextView as IWpfTextView;
            if (textView == null)
            {
                return;
            }

            // Setup the block caret 
            var caret = _blockCaretFactoryService.CreateBlockCaret(textView);
            var caretController = new BlockCaretController(buffer, caret);

            buffer.LocalSettings.SettingChanged += (_, args) => OnSettingChanged(buffer, args);
        }

        private void OnSettingChanged(IVimBuffer buffer, Setting args)
        {
            if (args.Name == LocalSettingNames.CursorLineName)
            {
                var options = _editorOptionsFatoryService.GetOptions(buffer.TextView);
                options.SetOptionValue(DefaultWpfViewOptions.EnableHighlightCurrentLineId, buffer.LocalSettings.CursorLine);
            }
        }
    }
}
