using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;

namespace Vim.UI.Wpf.Implementation.Mouse
{
    internal sealed class VimMouseProcessor : MouseProcessorBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IKeyboardDevice _keyboardDevice;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IWpfTextView _wpfTextView;
        private readonly MouseDevice _mouseDevice;

        private bool _mouseCaptured;
        private DispatcherTimer _scrollTimer;

        internal VimMouseProcessor(
            IVimBuffer vimBuffer,
            IKeyboardDevice keyboardDevice,
            IProtectedOperations protectedOperations)
        {
            _vimBuffer = vimBuffer;
            _keyboardDevice = keyboardDevice;
            _protectedOperations = protectedOperations;
            _wpfTextView = (IWpfTextView)_vimBuffer.TextView;
            _mouseDevice = InputManager.Current.PrimaryMouseDevice;
            _mouseCaptured = false;
            _scrollTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, 1000),
                DispatcherPriority.Normal,
                protectedOperations.GetProtectedEventHandler(OnScrollTimer),
                Dispatcher.CurrentDispatcher);
        }

        internal bool TryProcess(VimKey vimKey, int clickCount = 1)
        {
            var keyInput = KeyInputUtil.ApplyKeyModifiersToKey(vimKey, _keyboardDevice.KeyModifiers);
            keyInput = KeyInputUtil.ApplyClickCount(keyInput, clickCount);

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

        public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // These methods get called for the entire mouse processing chain
            // before calling PreprocessMouseDown (and there is not an equivalent
            // for PreprocessMouseMiddleButtonDown).
            this.PreprocessMouseDown(e);
        }

        public override void PreprocessMouseRightButtonDown(MouseButtonEventArgs e)
        {
            // These methods get called for the entire mouse processing chain
            // before calling PreprocessMouseDown (and there is not an equivalent
            // for PreprocessMouseMiddleButtonDown).
            this.PreprocessMouseDown(e);
        }

        public override void PreprocessMouseDown(MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    e.Handled = TryProcess(VimKey.LeftMouse, e.ClickCount);
                    if (e.Handled)
                    {
                        CaptureMouse();
                    }
                    break;
                case MouseButton.Middle:
                    e.Handled = TryProcess(VimKey.MiddleMouse, e.ClickCount);
                    break;
                case MouseButton.Right:
                    e.Handled = TryProcess(VimKey.RightMouse, e.ClickCount);
                    break;
                case MouseButton.XButton1:
                    e.Handled = TryProcess(VimKey.X1Mouse, e.ClickCount);
                    break;
                case MouseButton.XButton2:
                    e.Handled = TryProcess(VimKey.X2Mouse, e.ClickCount);
                    break;
            }
        }

        public override void PreprocessMouseUp(MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.Left:
                    e.Handled = TryProcess(VimKey.LeftRelease);
                    ReleaseMouseCapture();
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
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                CheckScroll();
            }
            TryProcessDrag(e, e.MiddleButton, VimKey.RightDrag);
            TryProcessDrag(e, e.RightButton, VimKey.RightDrag);
            TryProcessDrag(e, e.XButton1, VimKey.X1Drag);
            TryProcessDrag(e, e.XButton2, VimKey.X2Drag);
        }

        private void CaptureMouse()
        {
            if (!_mouseCaptured)
            {
                _mouseDevice.Capture(_wpfTextView.VisualElement);
                _mouseCaptured = true;
                _scrollTimer.IsEnabled = true;
            }
        }

        private void ReleaseMouseCapture()
        {
            if (_mouseCaptured)
            {
                _wpfTextView.VisualElement.ReleaseMouseCapture();
                _mouseCaptured = false;
                _scrollTimer.IsEnabled = false;
            }
        }

        private void CheckScroll()
        {
            if (_mouseCaptured)
            {
            }
        }

        private void OnScrollTimer(object sender, EventArgs e)
        {
            if (!_mouseCaptured || !_wpfTextView.VisualElement.IsMouseCaptured)
            {
                _mouseCaptured = false;
                _scrollTimer.IsEnabled = false;
                return;
            }

            _wpfTextView.ViewScroller.ScrollViewportVerticallyByLines(ScrollDirection.Up, 1);
        }

    }
}
