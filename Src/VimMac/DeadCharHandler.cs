using AppKit;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Cocoa
{
    internal sealed class DeadCharHandler
    {
        private bool _lastEventWasDeadChar;
        private bool _processingDeadChar;
        private string _convertedDeadCharacters;
        private readonly ITextView _textView;
        private InvisibleTextView _invisibleTextView;

        public DeadCharHandler(ITextView textView)
        {
            _textView = textView;
        }

        public string ConvertedDeadCharacters => _convertedDeadCharacters;

        internal void SetConvertedDeadCharacters(string value)
        {
            _convertedDeadCharacters = value;
        }

        public bool LastEventWasDeadChar => _lastEventWasDeadChar;
        public bool ProcessingDeadChar => _processingDeadChar;

        public void InterpretEvent(NSEvent keyPress)
        {
            if (_convertedDeadCharacters != null)
            {
                // reset state
                _convertedDeadCharacters = null;
                _processingDeadChar = false;
            }

            _lastEventWasDeadChar = _processingDeadChar;

            _processingDeadChar = KeyEventIsDeadChar(keyPress);

            if (!_processingDeadChar && !_lastEventWasDeadChar)
            {
                return;
            }

            _invisibleTextView ??= new InvisibleTextView(this, _textView);
            // Send the cloned key press to the invisible NSTextView
            _invisibleTextView.InterpretEvent(keyPress);
        }

        private bool KeyEventIsDeadChar(NSEvent e)
        {
            return string.IsNullOrEmpty(e.Characters);
        }
    }
}
