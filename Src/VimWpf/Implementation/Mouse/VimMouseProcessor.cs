using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace Vim.UI.Wpf.Implementation.Mouse
{
    internal sealed class VimMouseProcessor : MouseProcessorBase
    {
        /// <summary>
        /// How many lines per second we should scroll when auto-scrolling
        /// </summary>
        private static readonly int s_linesPerSecond = 8;

        private readonly IVimBuffer _vimBuffer;
        private readonly IKeyboardDevice _keyboardDevice;
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
            _wpfTextView = (IWpfTextView)_vimBuffer.TextView;
            _mouseDevice = InputManager.Current.PrimaryMouseDevice;
            _mouseCaptured = false;
            _scrollTimer = new DispatcherTimer(
                new TimeSpan(0, 0, 0, 0, 1000 / s_linesPerSecond),
                DispatcherPriority.Normal,
                protectedOperations.GetProtectedEventHandler(OnScrollTimer),
                Dispatcher.CurrentDispatcher);
        }

        internal bool TryProcess(VimKey vimKey, int clickCount = 1)
        {
            var keyInput = KeyInputUtil.ApplyKeyModifiersToKey(vimKey, _keyboardDevice.KeyModifiers);
            keyInput = KeyInputUtil.ApplyClickCount(keyInput, clickCount);

            // If the user has explicitly set the mouse to be <nop> then we
            // don't want to report this as  handled.  Otherwise it will
            // swallow the mouse event and as a consequence disable other
            // features that begin with a mouse click.
            //
            // There is really no other way for the user to opt out of mouse
            // behavior besides mapping the  key to <nop> otherwise that would
            // be done here.
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

        private bool TryProcessDrag(MouseButtonState state, VimKey vimKey)
        {
            if (state == MouseButtonState.Pressed)
            {
                return TryProcess(vimKey);
            }
            return false;
        }

        public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            // These methods get called for the entire mouse processing chain
            // before calling PreprocessMouseDown (and there is not an
            // equivalent for PreprocessMouseMiddleButtonDown).
            this.PreprocessMouseDown(e);
        }

        public override void PreprocessMouseRightButtonDown(MouseButtonEventArgs e)
        {
            // These methods get called for the entire mouse processing chain
            // before calling PreprocessMouseDown (and there is not an
            // equivalent for PreprocessMouseMiddleButtonDown).
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
                    CheckReleaseMouseCapture();
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
            var handled = false;
            handled |= TryProcessDrag(e.LeftButton, VimKey.LeftDrag);
            handled |= TryProcessDrag(e.MiddleButton, VimKey.RightDrag);
            handled |= TryProcessDrag(e.RightButton, VimKey.RightDrag);
            handled |= TryProcessDrag(e.XButton1, VimKey.X1Drag);
            handled |= TryProcessDrag(e.XButton2, VimKey.X2Drag);
            if (handled)
            {
                e.Handled = true;
            }
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

        private void CheckReleaseMouseCapture()
        {
            if (_mouseCaptured)
            {
                _wpfTextView.VisualElement.ReleaseMouseCapture();
                _mouseCaptured = false;
                _scrollTimer.IsEnabled = false;
            }
        }

        private void OnScrollTimer(object sender, EventArgs e)
        {
            // Don't attempt to scroll the text view during a layout.
            if (_wpfTextView.InLayout)
            {
                return;
            }

            // Make sure the timer event is still relevant.
            if (_wpfTextView.IsClosed
                || !_wpfTextView.HasAggregateFocus
                || !_mouseCaptured
                || !_wpfTextView.VisualElement.IsMouseCaptured
                || _mouseDevice.LeftButton != MouseButtonState.Pressed)
            {
                _mouseCaptured = false;
                _scrollTimer.IsEnabled = false;
                return;
            }

            // Check whether we should scroll the text view.
            var mousePoint = _mouseDevice.GetPosition(_wpfTextView.VisualElement);
            var lineHeight = _wpfTextView.LineHeight;
            var scrollDirection = null as ScrollDirection?;
            var lines = 1;
            if (mousePoint.Y < 0)
            {
                if (!GetIsFirstLineVisible())
                {
                    scrollDirection = ScrollDirection.Up;
                    lines = (int)Math.Ceiling((0 - mousePoint.Y) / lineHeight);
                }
            }
            if (mousePoint.Y > _wpfTextView.ViewportHeight)
            {
                if (!GetIsLastLineVisible())
                {
                    scrollDirection = ScrollDirection.Down;
                    lines = (int)Math.Ceiling((mousePoint.Y - _wpfTextView.ViewportHeight) / lineHeight);
                }
            }

            // If we should scroll, scroll the text view and process a virtual
            // drag event.
            if (scrollDirection.HasValue)
            {
                _wpfTextView.ViewScroller.ScrollViewportVerticallyByLines(scrollDirection.Value, lines);
                TryProcessDrag(MouseButtonState.Pressed, VimKey.LeftDrag);
            }
        }

        private bool GetIsFirstLineVisible()
        {
            // Check whether the first line is visible.
            return _wpfTextView.ViewportTop == 0;
        }

        private bool GetIsLastLineVisible()
        {
            // Check whether the last line is visible.
            try
            {
                var textViewLines = _wpfTextView.TextViewLines;
                if (textViewLines != null && textViewLines.IsValid)
                {
                    var lastVisibleTextViewLine = textViewLines.LastVisibleLine;
                    var lastVisibleLine = lastVisibleTextViewLine.Start.GetContainingLine();
                    var lastVisibleLineNumber = lastVisibleLine.LineNumber;
                    var snapshot = lastVisibleLine.Snapshot;
                    if (lastVisibleLineNumber + 1 == snapshot.LineCount)
                    {
                        // Make sure the whole line is visible.
                        var endOfLastVisibleLine = lastVisibleTextViewLine.EndIncludingLineBreak;
                        if (endOfLastVisibleLine.Position == snapshot.Length)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                VimTrace.TraceError(ex);
            }
            return false;
        }
    }
}
