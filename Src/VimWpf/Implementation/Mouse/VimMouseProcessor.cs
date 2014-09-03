using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Vim.UI.Wpf.Implementation.Mouse
{
    internal sealed class VimMouseProcessor : MouseProcessorBase
    {
        private readonly IVimBuffer _vimBuffer;

        internal VimMouseProcessor(IVimBuffer vimBuffer)
        {
            _vimBuffer = vimBuffer;
        }

        internal bool TryProcess(VimKey vimKey)
        {
            var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);

            // If the user has explicitly set the mouse to be <nop> then we don't want to report this as 
            // handled.  Otherwise it will swallow the mouse event and as a consequence disable other
            // features that begin with a mouse click.  
            //
            // There is really no other way for the user to opt out of mouse behavior besides mapping the 
            // key to <nop> otherwise that would be done here.  
            var keyInputSet = _vimBuffer.GetKeyInputMapping(keyInput).KeyInputSet;
            if (keyInputSet.Length > 0 && keyInputSet.KeyInputs[0].Key == VimKey.Nop)
            {
                return false;
            }

            if (_vimBuffer.CanProcess(keyInput))
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            return false;
        }

        private void TryProcessDrag(MouseEventArgs e, MouseButtonState state, VimKey vimKey)
        {
            if (state == MouseButtonState.Pressed)
            {
                e.Handled = TryProcess(vimKey);
            }
        }

        public override void PreprocessMouseDown(MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    e.Handled = TryProcess(VimKey.LeftMouse);
                    break;
                case MouseButton.Middle:
                    e.Handled = TryProcess(VimKey.MiddleMouse);
                    break;
                case MouseButton.Right:
                    e.Handled = TryProcess(VimKey.RightMouse);
                    break;
                case MouseButton.XButton1:
                    e.Handled = TryProcess(VimKey.X1Mouse);
                    break;
                case MouseButton.XButton2:
                    e.Handled = TryProcess(VimKey.X2Mouse);
                    break;
            }
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    e.Handled = TryProcess(VimKey.LeftRelease);
                    break;
                case MouseButton.Middle:
                    e.Handled = TryProcess(VimKey.MiddleRelease);
                    break;
                case MouseButton.Right:
                    e.Handled = TryProcess(VimKey.RightRelease);
                    break;
                case MouseButton.XButton1:
                    e.Handled = TryProcess(VimKey.X1Release);
                    break;
                case MouseButton.XButton2:
                    e.Handled = TryProcess(VimKey.X2Release);
                    break;
            }
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            TryProcessDrag(e, e.LeftButton, VimKey.LeftDrag);
            TryProcessDrag(e, e.MiddleButton, VimKey.RightDrag);
            TryProcessDrag(e, e.RightButton, VimKey.RightDrag);
            TryProcessDrag(e, e.XButton1, VimKey.X1Drag);
            TryProcessDrag(e, e.XButton2, VimKey.X2Drag);
        }
    }
}
