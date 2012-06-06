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
    /// </summary>
    internal sealed class VsKeyProcessor : VimKeyProcessor
    {
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
            if (IsDiscardedKeyInput(keyInput))
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
            if (VimBuffer.ModeKind.IsAnyInsert() && !VimBuffer.CanProcessAsCommand(keyInput))
            {
                return false;
            }

            return base.TryProcess(keyInput);
        }

        /// <summary>
        /// Is this KeyInput value to be discarded based on previous KeyInput values
        /// </summary>
        private bool IsDiscardedKeyInput(KeyInput keyInput)
        {
            // Check to see if we should be discarding this KeyInput value.  If the KeyInput matches
            // then we mark the KeyInput as handled since it's the value we want to discard.  In either
            // case though we clear out the discarded KeyInput value.  This value is only meant to
            // last for a single key stroke.
            var isDiscarded = _bufferCoordinator.DiscardedKeyInput.IsSome(keyInput);
            _bufferCoordinator.DiscardedKeyInput = FSharpOption<KeyInput>.None;

            return isDiscarded;
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
