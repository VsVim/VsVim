using AppKit;
using Foundation;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Cocoa
{
    /// <summary>
    /// An invisible NSTextView used so we can leverage macOS to handle
    /// the conversion of keycodes and dead key presses into characters for
    /// any keyboard layout.
    /// </summary>
    internal sealed class InvisibleTextView : NSTextView
    {
        private bool _lastEventWasDeadChar;
        private bool _processingDeadChar;
        private string _convertedDeadCharacters;
        private ITextBuffer _textBuffer;

        public InvisibleTextView(ITextView textView)
        {
            _textBuffer = textView.TextBuffer;
            textView.TextBuffer.Changing += TextBuffer_Changing;
            textView.Closed += TextView_Closed;
        }

        private void TextBuffer_Changing(object sender, TextContentChangingEventArgs e)
        {
            if(_processingDeadChar)
            {
                // We need the dead key press event to register so that we get the correct
                // subsequent keypress vents, but we don't want to modify the textbuffer contents.
                e.Cancel();
            }
        }

        public string ConvertedDeadCharacters => _convertedDeadCharacters;

        public void InterpretEvent(NSEvent keyPress)
        {
            _convertedDeadCharacters = null;
            _lastEventWasDeadChar = _processingDeadChar;
            _processingDeadChar = string.IsNullOrEmpty(keyPress.Characters);

            var keypressCloned = NSEvent.KeyEvent(keyPress.Type, keyPress.LocationInWindow, keyPress.ModifierFlags, keyPress.Timestamp, keyPress.WindowNumber, keyPress.Context, keyPress.Characters, keyPress.CharactersIgnoringModifiers, keyPress.IsARepeat, keyPress.KeyCode);
            // Send the cloned key press to this NSTextView
            InterpretKeyEvents(new[] { keypressCloned });
        }

        public override void InsertText(NSObject text, NSRange replacementRange)
        {
            if (_lastEventWasDeadChar && !_processingDeadChar)
            {
                // This is where we find out how the combination of keypresses
                // has been interpreted.
                _convertedDeadCharacters = text.ToString();
            }
        }

        private void TextView_Closed(object sender, System.EventArgs e)
        {
            _textBuffer.Changing -= TextBuffer_Changing;
        }
    }
}
