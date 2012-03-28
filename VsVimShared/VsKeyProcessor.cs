using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Vim;
using Vim.UI.Wpf;

namespace VsVim
{
    /// <summary>
    /// This is the Visual Studio specific implementation of the typical Vim  key processor.  The
    /// base key processor is sufficient to actually handle most types of input.  Unfortunately 
    /// there are Visual Studio specific quirks we need to handle.  
    ///
    /// One such quirk is the TranslateAccelorator call.  This happens after the KeyDown event but
    /// before TextInput.  It goes several core Visual Studio input routes and in cases where the 
    /// buffer is readonly the event will be swallowed (sometimes loud, sometimes silently).  This
    /// behavior needs to be special cased here so Vim commands don't trigger the loud behavior
    /// </summary>
    internal sealed class VsKeyProcessor : KeyProcessor
    {
        private static readonly HashSet<char> CoreCharacterSet = new HashSet<char>(KeyInputUtil.VimKeyCharList);
        private readonly IVsAdapter _adapter;
        private readonly IVimBufferCoordinator _bufferCoordinator;

        internal VsKeyProcessor(IVsAdapter adapter, IVimBufferCoordinator bufferCoordinator)
            : base(bufferCoordinator.VimBuffer)
        {
            _adapter = adapter;
            _bufferCoordinator = bufferCoordinator;
        }

        /// <summary>
        /// This method is called to process KeyInput in any fashion by the IVimBuffer.  There are 
        /// several cases where we want to defer to Visual Studio and IOleCommandTarget for processing
        /// of a command.  In particular we don't want to process any text input here
        /// </summary>
        protected override bool TryProcess(KeyInput keyInput)
        {
            if (TryProcessCore(keyInput))
            {
                return true;
            }

            // Don't handle input when incremental search is active.  Let Visual Studio handle it
            if (_adapter.IsIncrementalSearchActive(TextView))
            {
                return false;
            }

            // In insert mode we don't want text input going directly to VsVim.  Text input must
            // be routed through Visual Studio and IOleCommandTarget in order to get intellisense
            // properly hooked up.  Not handling it in this KeyProcessor will eventually cause
            // it to be routed through IOleCommandTarget if it's input
            if (VimBuffer.ModeKind.IsAnyInsert())
            {
                return false;
            }

            return base.TryProcess(keyInput);
        }

        /// <summary>
        /// Make sure we respect silently handled input here
        /// </summary>
        protected override bool TryProcessAsCommand(KeyInput keyInput)
        {
            if (TryProcessCore(keyInput))
            {
                return true;
            }

            // Don't handle input when incremental search is active.  Let Visual Studio handle it
            if (_adapter.IsIncrementalSearchActive(TextView))
            {
                return false;
            }

            return base.TryProcessAsCommand(keyInput);
        }

        /// <summary>
        /// Implement the common logic for TryProcess as it relates to Visual Studio specific 
        /// input
        /// </summary>
        private bool TryProcessCore(KeyInput keyInput)
        {
            // Check to see if we should be discarding this KeyInput value.  If the KeyInput matches
            // then we mark the KeyInput as handled since it's the value we want to discard.  In either
            // case though we clear out the discarded KeyInput value.  This value is only meant to
            // last for a single key stroke.
            var handled = _bufferCoordinator.DiscardedKeyInput.IsSome(keyInput);
            _bufferCoordinator.DiscardedKeyInput = FSharpOption<KeyInput>.None;

            return handled;
        }

        /// <summary>
        /// Handle the case where we need to process a KeyDown but it will be swallowed by the 
        /// TranslateAccelorator chain of input
        ///
        /// Must be **very** careful here because of international key board issues.  The base 
        /// KeyProcessor in VimWPF has ample documentation on why this is dangerous.  In short 
        /// though we must be as specific as possible when choosing keys to filter out because 
        /// mapping a Key at this level to a Vim KeyInput with 100% accuracey is not possible 
        /// </summary>
        public override void KeyDown(KeyEventArgs args)
        {
            base.KeyDown(args);
            if (args.Handled)
            {
                return;
            }

            // Don't attempt to handle this if we're in an incremental search
            if (_adapter.IsIncrementalSearchActive(TextView))
            {
                return;
            }

            // Don't process anything unless we're in a case where TranslateAccelorator would 
            // win.  Also get rid of the problem cases from the start
            if (!_adapter.IsReadOnly(TextBuffer) ||
                !KeyUtil.IsInputKey(args.Key) ||
                KeyUtil.IsAltGr(args.KeyboardDevice.Modifiers))
            {
                return;
            }

            var handled = false;
            KeyInput keyInput;
            if (KeyUtil.TryConvertToKeyInput(args.Key, args.KeyboardDevice.Modifiers, out keyInput))
            {
                // We only want to process input characters here.  All other input will eventually 
                // be routed along a more reliable route for us to convert back to Vim KeyInput
                if (keyInput.KeyModifiers == KeyModifiers.None && KeyUtil.IsMappedByChar(keyInput.Key) && CoreCharacterSet.Contains(keyInput.Char))
                {
                    // We intentionally avoid using the TryProcess version here.  This is one case
                    // we don't want to defer to Visual Studio.  It thinks the buffer is readonly and 
                    // will react as such while we want to do actual commands here
                    handled = VimBuffer.CanProcess(keyInput) && VimBuffer.Process(keyInput).IsAnyHandled;
                }
            }

            args.Handled = handled;
        }

        /// <summary>
        /// Once the key goes up the KeyStroke is complete and we should clear out the 
        /// DiscardedKeyInput flag as it's only relevant for a single key stroke
        /// </summary>
        public override void KeyUp(KeyEventArgs args)
        {
            _bufferCoordinator.DiscardedKeyInput = FSharpOption<KeyInput>.None;
            base.KeyUp(args);
        }
    }
}
