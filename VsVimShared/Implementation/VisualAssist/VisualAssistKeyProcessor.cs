using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.VisualAssist
{
    internal sealed class VisualAssistKeyProcessor : KeyProcessor
    {
        private readonly IVimBuffer _vimBuffer;

        internal VisualAssistKeyProcessor(IVimBuffer vimBuffer)
        {
            _vimBuffer = vimBuffer;
        }

        /// <summary>
        /// The escape key was pressed.  If we are currently in insert mode we need to leave it because it 
        /// means that Visual Assist swallowed the key stroke
        /// </summary>
        public override void PreviewKeyUp(KeyEventArgs args)
        {
            if (args.Key == Key.Escape ||
                (args.Key == Key.OemOpenBrackets && args.KeyboardDevice.Modifiers == ModifierKeys.Control))
            {
                HandleEscape();
            }
            base.KeyDown(args);
        }

        /// <summary>
        /// The escape key was pressed.  If we are currently in insert mode we need to leave it because it 
        /// means that Visual Assist swallowed the key stroke
        /// </summary>
        private void HandleEscape()
        {
            if (_vimBuffer.ModeKind == ModeKind.Insert)
            {
                _vimBuffer.Process(KeyInputUtil.EscapeKey);
            }
        }
    }
}
