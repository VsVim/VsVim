using System.Collections.Generic;
using System.Windows.Input;
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
    /// behavior needs to be special cased here
    /// </summary>
    internal sealed class VsKeyProcessor : KeyProcessor
    {
        private static readonly HashSet<char> CoreCharacterSet = new HashSet<char>(KeyInputUtil.VimKeyCharList);
        private readonly IVsAdapter _adapter;

        /// <summary>
        /// There are several cases where we need to ignore text input and instead let the input 
        /// forward to the base.  
        ///   - During a visual studio incremental search operation we want them to get the input
        ///   - In insert mode we don't want text input going directly to VsVim.  Text input must
        ///     be routed through Visual Studio and IOleCommandTarget in order to get intellisense
        ///     properly hooked up
        /// </summary>
        protected override bool IgnoreTextInput
        {
            get
            {
                return
                    _adapter.IsIncrementalSearchActive(TextView) ||
                    VimBuffer.ModeKind == ModeKind.Insert ||
                    VimBuffer.ModeKind == ModeKind.Replace;
            }
        }

        internal VsKeyProcessor(IVsAdapter adapter, IVimBuffer buffer)
            : base(buffer)
        {

            _adapter = adapter;
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
            // Don't intercept keystrokes if Visual Studio IncrementalSearch is active
            if (_adapter.IsIncrementalSearchActive(TextView))
            {
                return;
            }

            base.KeyDown(args);
            if (args.Handled)
            {
                return;
            }

            // Don't process anything unless we're in a case where TranslateAccelorator would 
            // win.  Also get rid of the problem cases from the start
            if (!_adapter.IsReadOnly(TextBuffer)
                || !KeyUtil.IsInputKey(args.Key)
                || KeyUtil.IsAltGr(args.KeyboardDevice.Modifiers))
            {
                return;
            }

            var handled = false;
            KeyInput ki;
            if (KeyUtil.TryConvertToKeyInput(args.Key, args.KeyboardDevice.Modifiers, out ki))
            {
                // We only want to process input characters here.  All other input will eventually 
                // be routed along a more reliable route for us to convert back to Vim KeyInput
                if (ki.KeyModifiers == KeyModifiers.None &&  KeyUtil.IsMappedByChar(ki.Key) &&  CoreCharacterSet.Contains(ki.Char))
                {
                    handled = VimBuffer.CanProcess(ki) && VimBuffer.Process(ki).IsAnyHandled;
                }
            }

            args.Handled = handled;
        }
    }
}
