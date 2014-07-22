using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace Vim.VisualStudio.Implementation.VisualAssist
{
    internal sealed class VisualAssistKeyProcessor : KeyProcessor
    {
        private readonly IVimBuffer _vimBuffer;

        internal VisualAssistKeyProcessor(IVimBuffer vimBuffer)
        {
            _vimBuffer = vimBuffer;
        }

        /// <summary>
        /// The PreviewKeyDown event is raised before Visual Assist handles a key stroke and they respect the
        /// Handled property of the KeyEventArgs structure.  This is our chance to intercept any keys that
        /// they will process in a way that conflicts with VsVim
        /// </summary>
        public override void PreviewKeyDown(KeyEventArgs args)
        {
            VimTrace.TraceInfo("VisualAssistKeyProcessor::PreviewKeyDown {0} {1}", args.Key, args.KeyboardDevice.Modifiers);

            if (_vimBuffer.ModeKind == ModeKind.Normal && args.Key == Key.OemPeriod && args.KeyboardDevice.Modifiers == ModifierKeys.None)
            {
                // Visual Assist in general won't process any keys when we are in normal mode because we have 
                // the caret hidden.  However it appears they check for the caret hidden on a timer or some
                // form of delay.  This is provable by editting some text in insert mode then quickly hitting
                // Escape followed by '.'.  If this happens fast enough they will process the '.' directly 
                // instead of letting the key stroke go through.   This will cause a '.' to appear in the code
                // instead of a repeat action.  
                //
                // Experimentation shows that they only do this processing for a subset of keys including
                // '.'.  Letter keys and the like don't have this behavior so we let them go through
                // normal processing.  In the future though we may find more keys that need this exception
                _vimBuffer.Process(KeyInputUtil.CharToKeyInput('.'));
                args.Handled = true;
            }

            VimTrace.TraceInfo("VisualAssistKeyProcessor::KeyDown Handled = {0}", args.Handled);
            base.PreviewKeyDown(args);
        }

        /// <summary>
        /// The escape key was pressed.  If we are currently in insert mode we need to leave it because it 
        /// means that Visual Assist swallowed the key stroke to perform an operation like escaping 
        /// </summary>
        public override void PreviewKeyUp(KeyEventArgs args)
        {
            VimTrace.TraceInfo("VisualAssistKeyProcessor::PreviewKeyUp {0} {1}", args.Key, args.KeyboardDevice.Modifiers);
            if (args.Key == Key.Escape ||
                (args.Key == Key.OemOpenBrackets && args.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                // The Escape key was pressed and we are still inside of Insert mode.  This means that Visual Assit
                // handled the key stroke to dismiss intellisense.  Leave insert mode now to complete the operation
                if (_vimBuffer.ModeKind == ModeKind.Insert)
                {
                    VimTrace.TraceInfo("VisualAssistKeyProcessor::PreviewKeyUp handled escape swallowed by Visual Assist");
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                }
            }

            base.KeyDown(args);
        }
    }
}
