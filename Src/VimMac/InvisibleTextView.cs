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
        private readonly DeadCharHandler _deadCharHandler;
        private readonly ITextBuffer _textBuffer;

        public InvisibleTextView(DeadCharHandler deadCharHandler, ITextView textView)
        {
            _deadCharHandler = deadCharHandler;
            _textBuffer = textView.TextBuffer;
            textView.TextBuffer.Changing += TextBuffer_Changing;
            textView.Closed += TextView_Closed;
        }

        public void InterpretEvent(NSEvent keypress)
        {
            InterpretKeyEvents(new[] { CloneEvent(keypress) });
        }

        public override void InsertText(NSObject text, NSRange replacementRange)
        {
            if (_deadCharHandler.LastEventWasDeadChar && !_deadCharHandler.ProcessingDeadChar)
            {
                // This is where we find out how the combination of keypresses
                // has been interpreted.
                _deadCharHandler.SetConvertedDeadCharacters((text as NSString)?.ToString());
            }
        }

        private void TextBuffer_Changing(object sender, TextContentChangingEventArgs e)
        {
            if (_deadCharHandler.LastEventWasDeadChar || _deadCharHandler.ProcessingDeadChar)
            {
                // We need the dead key press event to register in the editor so
                // that we get the correct subsequent keypress events, but we 
                // don't want to modify the textbuffer contents.
                e.Cancel();
            }
        }

        private void TextView_Closed(object sender, System.EventArgs e)
        {
            _textBuffer.Changing -= TextBuffer_Changing;
        }

        private NSEvent CloneEvent(NSEvent keyPress)
        {
            return NSEvent.KeyEvent(
                keyPress.Type,
                keyPress.LocationInWindow,
                keyPress.ModifierFlags,
                keyPress.Timestamp,
                keyPress.WindowNumber,
                keyPress.Context,
                keyPress.Characters,
                keyPress.CharactersIgnoringModifiers,
                keyPress.IsARepeat,
                keyPress.KeyCode);
        }
    }
}
