using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace VsVim
{
    ///<summary>
    ///BlockCursor adornment places red boxes behind all the "a"s in the editor window
    ///</summary>
    public class BlockCursor
    {
        [DllImport("user32.dll")]
        private static extern int GetCaretBlinkTime();

        private IAdornmentLayer _layer;
        private IWpfTextView _view;
        private Pen _pen;
        private object _tag = Guid.NewGuid().ToString();
        private Image _caretImage;
        private DispatcherTimer _blinkTimer;

        public BlockCursor(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("BlockCursor");

            //Listen to any event that changes the layout (text changes, scrolling, etc)
            _view.LayoutChanged += OnLayoutChanged;
            _view.Caret.PositionChanged += OnCaretChanged;

            _view.Caret.IsHidden = false;
        }

        /// <summary>
        /// On layout change add the adornment to any reformated lines
        /// </summary>
        private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretChanged(object sender, CaretPositionChangedEventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretBlinkTimer(object sender, EventArgs e)
        {
            if (_caretImage != null)
            {
                _caretImage.Visibility = _caretImage.Visibility == Visibility.Visible
                    ? Visibility.Hidden
                    : Visibility.Visible;
            }
        }

        private void UpdateCaret()
        {
            var caret = _view.Caret;
            var line = caret.ContainingTextViewLine;
            if (line.VisibilityState == VisibilityState.Unattached)
            {
                // Not Visible
                MaybeDestroyCaret();
            }
            else
            {
                if (_caretImage == null)
                {
                    CreateCaretImage();
                }
                else
                {
                    if (!_blinkTimer.IsEnabled)
                    {
                        _blinkTimer.IsEnabled = true;
                    }
                    MoveCaretImageToCaret();
                }
            }
        }

        private void MaybeDestroyCaret()
        {
            if (_caretImage != null)
            {
                DestroyCaret();
            }
        }

        private void DestroyCaret()
        {
            _layer.RemoveAdornmentsByTag(_tag);
            _blinkTimer.IsEnabled = false;
            _blinkTimer = null;
        }

        private void MoveCaretImageToCaret()
        {
            var caret = _view.Caret;
            Canvas.SetLeft(_caretImage, caret.Left);
            Canvas.SetTop(_caretImage, caret.Top);
        }

        private void CreateCaretImage()
        {
            //Create the pen and brush to color the box behind the a's
            Brush penBrush = new SolidColorBrush(Colors.Green);
            penBrush.Freeze();
            Pen pen = new Pen(penBrush, 1.0);
            pen.Freeze();

            var line = _view.Caret.ContainingTextViewLine;
            var topLeft = new Point(_view.Caret.Left, _view.Caret.Top);
            var rect = new Rect(topLeft, new Size(10, 10));
            var g = new RectangleGeometry(rect);
            var drawing = new GeometryDrawing(Brushes.Green, _pen, g);
            drawing.Freeze();

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            _caretImage = new Image();
            _caretImage.Source = drawingImage;

            _blinkTimer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(GetCaretBlinkTime()),
                DispatcherPriority.Normal,
                new EventHandler(OnCaretBlinkTimer),
                _caretImage.Dispatcher);
            _blinkTimer.IsEnabled = true;

            _layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                new SnapshotSpan(_view.Caret.Position.BufferPosition, 0),
                _tag,
                _caretImage,
                (x, y) =>
                {
                    _caretImage = null;
                    _blinkTimer = null;
                });

            MoveCaretImageToCaret();
        }
    }
}
