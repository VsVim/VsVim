using AppKit;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using MonoDevelop.Ide;
using Vim.UI.Cocoa.Implementation.InlineRename;

namespace Vim.UI.Cocoa
{

    /// <summary>
    /// The morale of the history surrounding this type is translating key input is
    /// **hard**.  Anytime it's done manually and expected to be 100% correct it 
    /// likely to have a bug.  If you doubt this then I encourage you to read the 
    /// following 10 part blog series
    /// 
    /// http://blogs.msdn.com/b/michkap/archive/2006/04/13/575500.aspx
    ///
    /// Or simply read the keyboard feed on the same blog page.  It will humble you
    /// </summary>
    internal sealed class VimKeyProcessor : KeyProcessor
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly ITextView _textView;
        private readonly IKeyUtil _keyUtil;
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;
        private readonly InlineRenameListenerFactory _inlineRenameListenerFactory;
        private readonly InvisibleTextView _invisibleTextView;

        public VimKeyProcessor(
            IVimBuffer vimBuffer,
            IKeyUtil keyUtil,
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker,
            InlineRenameListenerFactory inlineRenameListenerFactory)

        {
            _vimBuffer = vimBuffer;
            _textView = vimBuffer.TextView;
            _keyUtil = keyUtil;
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            _inlineRenameListenerFactory = inlineRenameListenerFactory;
            _invisibleTextView = new InvisibleTextView(_textView);
        }

        public override bool IsInterestedInHandledEvents => true;

        /// <summary>
        /// This handler is necessary to intercept keyboard input which maps to Vim
        /// commands but doesn't map to text input.  Any combination which can be 
        /// translated into actual text input will be done so much more accurately by
        /// WPF and will end up in the TextInput event.
        /// 
        /// An example of why this handler is needed is for key combinations like 
        /// Shift+Escape.  This combination won't translate to an actual character in most
        /// (possibly all) keyboard layouts.  This means it won't ever make it to the
        /// TextInput event.  But it can translate to a Vim command or mapped keyboard 
        /// combination that we do want to handle.  Hence we override here specifically
        /// to capture those circumstances
        /// </summary>
        public override void KeyDown(KeyEventArgs e)
        {
            VimTrace.TraceInfo("VimKeyProcessor::KeyDown {0} {1}", e.Characters, e.CharactersIgnoringModifiers);

            bool handled = false;

            _invisibleTextView.InterpretEvent(e.Event);
            if (KeyEventIsDeadChar(e))
            {
                // Although there is nothing technically left to do, we still
                // need to make sure that the event is processed by the
                // underlying NSView so that InterpretKeyEvents is called
                handled = false;
            }
            else
            {
                if (_invisibleTextView.ConvertedDeadCharacters != null)
                {
                    foreach (var c in _invisibleTextView.ConvertedDeadCharacters)
                    {
                        var key = KeyInputUtil.ApplyKeyModifiersToChar(c, VimKeyModifiers.None);
                        handled &= TryProcess(null, key);
                    }
                }
                else
                {
                    // Attempt to map the key information into a KeyInput value which can be processed
                    // by Vim.  If this works and the key is processed then the input is considered
                    // to be handled
                    bool canConvert = _keyUtil.TryConvertSpecialToKeyInput(e.Event, out KeyInput keyInput);
                    if (canConvert)
                    {
                        handled = TryProcess(e, keyInput);
                    }
                }
            }
            VimTrace.TraceInfo("VimKeyProcessor::KeyDown Handled = {0}", handled);

            var status = Mac.StatusBar.GetStatus(_vimBuffer);
            var text = status.Text;
            if (_vimBuffer.ModeKind == ModeKind.Command)
            {
                // Add a fake 'caret'
                text = text.Insert(status.CaretPosition, "|");
            }
            IdeApp.Workbench.StatusBar.ShowMessage(text);
            e.Handled = handled;
        }

        private bool KeyEventIsDeadChar(KeyEventArgs e)
        {
            return string.IsNullOrEmpty(e.Characters);
        }

        private bool IsEscapeKey(KeyEventArgs e)
        {
            return (NSKey)e.Event.KeyCode == NSKey.Escape;
        }

        private bool TryProcess(KeyEventArgs e, KeyInput keyInput)
        {
            if (e != null && KeyEventIsDeadChar(e))
                // When a dead key combination is pressed we will get the key down events in 
                // sequence after the combination is complete.  The dead keys will come first
                // and be followed the final key which produces the char.  That final key 
                // is marked as DeadCharProcessed.
                //
                // All of these should be ignored.  They will produce a TextInput value which
                // we can process in the TextInput event
                return false;

            if ((_vimBuffer.ModeKind.IsAnyInsert() || _vimBuffer.ModeKind.IsAnySelect()) &&
                !_vimBuffer.CanProcessAsCommand(keyInput) &&
                keyInput.Char > 0x1f &&
                _vimBuffer.BufferedKeyInputs.IsEmpty &&
                !_vimBuffer.Vim.MacroRecorder.IsRecording)
                return false;

            if (_completionBroker.IsCompletionActive(_textView) && !IsEscapeKey(e))
                return false;

            if (_signatureHelpBroker.IsSignatureHelpActive(_textView) && !IsEscapeKey(e))
                return false;

            if (_inlineRenameListenerFactory.InRename)
                return false;

            if (_vimBuffer.ModeKind.IsAnyInsert() && e?.Characters == "\t")
                // Allow tab key to work for snippet completion
                //
                // TODO: We should only really do this when the characters
                // to the left of the caret form a valid snippet
                return false;

            return _vimBuffer.CanProcess(keyInput) && _vimBuffer.Process(keyInput).IsAnyHandled;
        }
    }
}
