using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows.Input;
using IOPath = System.IO.Path;

namespace Vim.UI.Wpf.Implementation.Directory
{
    internal sealed class DirectoryKeyProcessor : KeyProcessor
    {
        private readonly string _directoryPath;
        private readonly IVimHost _vimHost;
        private readonly ITextView _textView;

        internal DirectoryKeyProcessor(string directoryPath, IVimHost vimHost, ITextView textView)
        {
            _directoryPath = directoryPath;
            _vimHost = vimHost;
            _textView = textView;
        }

        public override void KeyDown(KeyEventArgs args)
        {
            base.KeyDown(args);

            if (_textView.InLayout || args.Key != Key.Enter)
            {
                return;
            }

            var line = _textView.Caret.Position.BufferPosition.GetContainingLine();
            var name = line.GetText().TrimEnd('/');
            var filePath = IOPath.Combine(_directoryPath, name);
            _vimHost.LoadFileIntoExistingWindow(filePath, _textView);
            args.Handled = true;
        }
    }
}
