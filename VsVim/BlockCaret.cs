using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using VimCore;

namespace VsVim
{
    ///<summary>
    ///BlockCursor adornment places red boxes behind all the "a"s in the editor window
    ///</summary>
    public class BlockCursor : IBlockCaret
    {
        [DllImport("user32.dll")]
        private static extern int GetCaretBlinkTime();

        private double CaretOpacity = 0.65;

        private struct CaretData
        {
            /// <summary>
            /// Image being used to draw the caret
            /// </summary>
            internal readonly Image Image;

            /// <summary>
            /// Color used to create the brush
            /// </summary>
            internal readonly Color Color;

            /// <summary>
            /// Point this caret is tracking
            /// </summary>
            internal readonly SnapshotPoint Point;

            internal CaretData(Image image, Color color, SnapshotPoint point)
            {
                this.Image = image;
                this.Color = color;
                this.Point = point;
            }
        }

        private readonly IAdornmentLayer _layer;
        private readonly IWpfTextView _view;
        private readonly object _tag = Guid.NewGuid().ToString();
        private readonly DispatcherTimer _blinkTimer;

        /// <summary>
        /// Image being used to draw the caret.  Will be null if the caret is currently
        /// not displayed
        /// </summary>
        private CaretData? _caretData;

        /// <summary>
        /// Does the consumer of IBlockCaret want us to be in control of displaying the caret
        /// </summary>
        private bool _isShown;

        /// <summary>
        /// Is the real caret visible in some way
        /// </summary>
        private bool IsRealCaretVisible
        {
            get
            {
                var caret = _view.Caret;
                var line = caret.ContainingTextViewLine;
                return line.VisibilityState != VisibilityState.Unattached;
            }
        }

        public BlockCursor(IWpfTextView view, string adornmentLayer)
        {
            _view = view;
            _layer = view.GetAdornmentLayer(adornmentLayer);

            //Listen to any event that changes the layout (text changes, scrolling, etc)
            _view.LayoutChanged += OnLayoutChanged;
            _view.Caret.PositionChanged += OnCaretChanged;

            var caretBlinkTime = GetCaretBlinkTime();
            var caretBlinkTimeSpan = new TimeSpan(0, 0, 0, 0, caretBlinkTime);
            _blinkTimer = new DispatcherTimer(
                caretBlinkTimeSpan,
                DispatcherPriority.Normal,
                new EventHandler(OnCaretBlinkTimer),
                Dispatcher.CurrentDispatcher);
            _blinkTimer.IsEnabled = false;
        }

        private void OnCaretBlinkTimer(object sender, EventArgs e)
        {
            if (_isShown && _caretData.HasValue)
            {
                var data = _caretData.Value;
                data.Image.Opacity = data.Image.Opacity == 0.0
                    ? CaretOpacity
                    : 0.0;
            }
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

        private void UpdateCaret()
        {
            if (!_isShown)
            {
                return;
            }

            if (!IsRealCaretVisible)
            {
                MaybeDestroyCaret();
            }
            else if (NeedRecreateCaret())
            {
                MaybeDestroyCaret();
                CreateCaretData();
            }
            else
            {
                MoveCaretImageToCaret();
            }
        }

        private void MaybeDestroyCaret()
        {
            if ( _caretData.HasValue )
            {
                DestroyCaret();
            }
        }

        private bool NeedRecreateCaret()
        {
            if (!_caretData.HasValue)
            {
                return true;
            }

            var data = _caretData.Value;
            return  data.Color != GetRealCaretBrushColor() 
                || data.Point != _view.Caret.Position.BufferPosition ;
        }

        private void DestroyCaret()
        {
            _layer.RemoveAdornmentsByTag(_tag);
            _caretData = null;
        }

        private void MoveCaretImageToCaret()
        {
            var point = GetRealCaretVisualPoint();
            Canvas.SetLeft(_caretData.Value.Image, point.X);
            Canvas.SetTop(_caretData.Value.Image, point.Y);
        }

        private void CreateCaretData()
        {
            var brush = new SolidColorBrush(GetRealCaretBrushColor());
            brush.Freeze();
            var pen = new Pen(brush, 1.0);

            var rect = new Rect(
                GetRealCaretVisualPoint(),
                GetOptimalCaretSize());
            var geometry = new RectangleGeometry(rect);
            var drawing = new GeometryDrawing(brush, pen, geometry);
            drawing.Freeze();

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            var image = new Image();
            image.Opacity = CaretOpacity;
            image.Source = drawingImage;

            var point = _view.Caret.Position.BufferPosition;
            _caretData = new CaretData(image, GetRealCaretBrushColor(), point);
            _layer.AddAdornment(
               AdornmentPositioningBehavior.TextRelative,
               new SnapshotSpan(point, 0),
               _tag,
               image,
               (x, y) =>
               {
                   _caretData = null;
               });
            MoveCaretImageToCaret();
        }

        private Color GetRealCaretBrushColor()
        {
            // TODO :actually calculate it
            return Colors.Black;
        }

        private Point GetRealCaretVisualPoint()
        {
            return new Point(_view.Caret.Left, _view.Caret.Top);
        }

        /// <summary>
        /// Get the size that should be used for the caret
        /// </summary>
        private Size GetOptimalCaretSize()
        {
            var caret = _view.Caret;
            var line = caret.ContainingTextViewLine;
            var defaultSize = new Size(
                5,
                line.IsValid ? line.Height : 10);
            if (!IsRealCaretVisible)
            {
                return defaultSize;
            }

            var point = caret.Position.VirtualBufferPosition;
            var bounds = line.GetCharacterBounds(point);
            return new Size(
                bounds.Width,
                bounds.Height);
        }

        #region IBlockCaret

        public void Destroy()
        {
            Hide();
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Caret.PositionChanged -= OnCaretChanged;
        }

        public void Hide()
        {
            if (_isShown)
            {
                _isShown = false;
                _blinkTimer.IsEnabled = false;
                _view.Caret.IsHidden = false;
                DestroyCaret();
            }
        }

        public void Show()
        {
            if (!_isShown)
            {
                _isShown = true;
                if (IsRealCaretVisible)
                {
                    CreateCaretData();
                }
                _blinkTimer.IsEnabled = true;
                _view.Caret.IsHidden = true;
            }
        }

        public ITextView TextView
        {
            get { return _view; }
        }

        #endregion

    }
}
