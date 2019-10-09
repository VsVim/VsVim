using AppKit;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using MonoDevelop.Ide;
using Vim.Mac;

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
        private readonly IKeyUtil _keyUtil;

        private IVimBuffer VimBuffer { get; }
        private readonly ICompletionBroker _completionBroker;
        private readonly ISignatureHelpBroker _signatureHelpBroker;

        public ITextBuffer TextBuffer
        {
            get { return VimBuffer.TextBuffer; }
        }

        public ITextView TextView
        {
            get { return VimBuffer.TextView; }
        }

        public bool ModeChanged { get; private set; }

        public VimKeyProcessor(
            IVimBuffer vimBuffer,
            IKeyUtil keyUtil,
            ICompletionBroker completionBroker,
            ISignatureHelpBroker signatureHelpBroker)
        {
            VimBuffer = vimBuffer;
            _keyUtil = keyUtil;
            _completionBroker = completionBroker;
            _signatureHelpBroker = signatureHelpBroker;
            // TODO: We need to set the caret only after the text view has fully loaded
            // so that we can measure the text width
            CaretUtil.SetCaret(VimBuffer);
        }

        /// <summary>
        /// Try and process the given KeyInput with the IVimBuffer.  This is overridable by 
        /// derived classes in order for them to prevent any KeyInput from reaching the 
        /// IVimBuffer
        /// </summary>
        private bool TryProcess(KeyInput keyInput)
        {
            return VimBuffer.CanProcessAsCommand(keyInput) && VimBuffer.Process(keyInput).IsAnyHandled;
        }

        private bool KeyEventIsDeadChar(NSEvent e)
        {
            return string.IsNullOrEmpty(e.Characters);
        }

        private bool IsEscapeKey(NSEvent e)
        {
            return (NSKey)e.KeyCode == NSKey.Escape;
        }

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
        public override void KeyDown(NSEvent e)
        {
            VimTrace.TraceInfo("VimKeyProcessor::KeyDown {0} {1}", e.Characters, e.CharactersIgnoringModifiers);

            bool handled;
            if (KeyEventIsDeadChar(e))
            {
                // When a dead key combination is pressed we will get the key down events in 
                // sequence after the combination is complete.  The dead keys will come first
                // and be followed the final key which produces the char.  That final key 
                // is marked as DeadCharProcessed.
                //
                // All of these should be ignored.  They will produce a TextInput value which
                // we can process in the TextInput event
                handled = false;
            }
            else if (_completionBroker.IsCompletionActive(TextView) && !IsEscapeKey(e))
            {
                handled = false;
            }
            else if (_signatureHelpBroker.IsSignatureHelpActive(TextView))
            {
                handled = false;
            }
            else
            {
                var oldMode = VimBuffer.Mode.ModeKind;

                VimTrace.TraceDebug(oldMode.ToString());
                // Attempt to map the key information into a KeyInput value which can be processed
                // by Vim.  If this works and the key is processed then the input is considered
                // to be handled
                if (_keyUtil.TryConvertSpecialToKeyInput(e, out KeyInput keyInput))
                {
                    handled = TryProcess(keyInput);
                }
                else
                {
                    handled = false;
                }
                var newMode = VimBuffer.Mode.ModeKind;
                VimTrace.TraceDebug(newMode.ToString());
                if (oldMode != ModeKind.Insert)
                {
                    handled = true;
                }
                //e.Handled = handled;
            }

            VimTrace.TraceInfo("VimKeyProcessor::KeyDown Handled = {0}", handled);

            var message = Mac.StatusBar.GetStatus(VimBuffer).Text;
            IdeApp.Workbench.StatusBar.ShowMessage(message);
            //TODO: Hack so that ByPassKeyProcessorProvider can prevent
            // the editor from receiving the typed character
            // For VSMac 8.4, the editor should be able to stop propogation to
            // the editor from this event
            TextView.Properties["Handled"] = handled;
            CaretUtil.SetCaret(VimBuffer);
        }
    }
}
