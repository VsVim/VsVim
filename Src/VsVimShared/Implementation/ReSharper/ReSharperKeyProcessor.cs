using System.Windows.Input;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.ReSharper
{
    // TODO: Delete this? 
    internal sealed class ReSharperKeyProcessor : KeyProcessor
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;

        internal ReSharperKeyProcessor(IVimBufferCoordinator vimBufferCoordinator)
        {
            _vimBufferCoordinator = vimBufferCoordinator;
            _vimBuffer = vimBufferCoordinator.VimBuffer;
        }

        public override void PreviewKeyDown(KeyEventArgs args)
        {
            switch (args.Key)
            {
                case Key.Escape:
                    PreviewKeyDownEscape(args);
                    break;
                case Key.Back:
                    PreviewKeyDownBack(args);
                    break;
                case Key.Enter:
                    PreviewKeyDownEnter(args);
                    break;
            }

            base.PreviewKeyDown(args);
        }

        private void PreviewKeyDownEscape(KeyEventArgs args)
        {
            // Have to special case Escape here for insert mode.  R# is typically ahead of us on the IOleCommandTarget
            // chain.  If a completion window is open and we wait for Exec to run R# will be ahead of us and run
            // their Exec call.  This will lead to them closing the completion window and not calling back into
            // our exec leaving us in insert mode.
            var handled = false;
            if (_vimBuffer.ModeKind.IsAnyInsert())
            {
                handled = TryProcess(KeyInputUtil.EscapeKey);
            }

            // Have to special case Escape here for external edit mode because we want escape to get us back to 
            // normal mode.  However we do want this key to make it to R# as well since they may need to dismiss
            // intellisense
            if (_vimBuffer.ModeKind == ModeKind.ExternalEdit)
            {
                handled = TryProcess(KeyInputUtil.EscapeKey);
            }

            // If we handled it then discard the KeyInput so that we don't double process it.  We still want the 
            // KeyInput to make it's way to ReSharper though so that it can dismiss Intellisense so don't mark
            // the event as handled
            if (handled)
            {
                _vimBufferCoordinator.Discard(KeyInputUtil.EscapeKey);
            }
        }

        private void PreviewKeyDownBack(KeyEventArgs args)
        {
            // R# special cases both the Back command in various scenarios
            //
            //  - Back is special cased to delete matched parens in Exec.  
            //
            // In these scenarios we want to make sure that we handle the key in Vim.  If this 
            // handling succeeds then we want to prevent R# from seeing the command.  If they see
            // it they will process it and the result will be conflicting actions
            var keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Back);
            if (!_vimBuffer.ModeKind.IsAnyInsert() && TryProcess(keyInput))
            {
                _vimBufferCoordinator.Discard(keyInput);
                args.Handled = true;
            }
        }

        private void PreviewKeyDownEnter(KeyEventArgs args)
        {
            // R# special cases both the Back and Enter command in various scenarios
            //
            //  - Enter is special cased in XML doc comments presumably to do custom formatting 
            //  - Enter is suppressed during debugging in Exec.  Presumably this is done to avoid the annoying
            //    "Invalid ENC Edit" dialog during debugging.
            //
            // In these scenarios we want to make sure that we handle the key in Vim.  If this 
            // handling succeeds then we want to prevent R# from seeing the command.  If they see
            // it they will process it and the result will be conflicting actions
            var keyInput = KeyInputUtil.EnterKey;
            if (!_vimBuffer.ModeKind.IsAnyInsert() && TryProcess(keyInput))
            {
                _vimBufferCoordinator.Discard(keyInput);
                args.Handled = true;
            }
        }

        private bool TryProcess(KeyInput keyInput)
        {
            if (_vimBufferCoordinator.IsDiscarded(keyInput))
            {
                return false; ;
            }

            return _vimBuffer.Process(keyInput).IsAnyHandled;
        }
    }
}
