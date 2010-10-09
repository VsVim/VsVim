using System.Collections.Generic;
using System.Windows.Input;
using Vim;
using Vim.Extensions;
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
        private static readonly HashSet<char> _coreCharacterSet = new HashSet<char>(KeyInputUtil.CoreCharacterList);
        private readonly IVsAdapter _adapter;

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
                if (ki.RawChar.IsSome() && _coreCharacterSet.Contains(ki.Char))
                {
                    handled = VimBuffer.CanProcess(ki) && VimBuffer.Process(ki);
                }
            }

            args.Handled = handled;
        }
    }
}
