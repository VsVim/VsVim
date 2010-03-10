using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text.Formatting;
using System.Windows;

namespace Vim.UI.Wpf.Implementation
{
    internal sealed class BlockCaret : IBlockCaret
    {
        private struct CaretData
        {
            internal readonly Image Image;
            internal readonly Color? Color;
            internal readonly SnapshotPoint Point;

            internal CaretData(Image image, Color? color, SnapshotPoint point)
            {
                Image = image;
                Color = color;
                Point = point;
            }
        }

        private readonly ITextView _view;
        private readonly IEditorFormatMap _formatMap;
        private readonly IAdornmentLayer _layer;
        private readonly Object _tag = new object();
        private readonly DispatcherTimer _blinkTimer;
        private CaretData? _caretData;
        private bool _isShown;

        private const double _caretOpacity = 0.65;

        public ITextView TextView
        {
            get { return _view; }
        }

        public bool IsShown
        {
            get { return _isShown; }
        }

        /// <summary>
        /// Is the real caret visible in some way?
        /// </summary>
        private bool IsRealCaretVisible
        {
            get
            {
                try
                {
                    var caret = _view.Caret;
                    var line = caret.ContainingTextViewLine;
                    return line.VisibilityState != VisibilityState.Unattached;
                }
                catch (InvalidOperationException)
                {
                    // InvalidOperationException is thrown when we ask for ContainingTextViewLine and the view
                    // is not yet completely rendered.  It's safe to say at this point that the caret is not 
                    // visible
                    return false;
                }
            }
        }

        private bool NeedRecreateCaret
        {
            get
            {
                if (_caretData.HasValue)
                {
                    var data = _caretData.Value;
                    return data.Color != GetRealCaretBrushColor() || data.Point != _view.Caret.Position.BufferPosition;
                }
                else
                {
                    return true;
                }
            }
        }

        internal BlockCaret(ITextView view, IEditorFormatMap formatMap, IAdornmentLayer layer)
        {
            _view = view;
            _formatMap = formatMap;
            _layer = layer;

            _view.LayoutChanged += OnLayoutChanged;
            _view.Caret.PositionChanged += OnCaretChanged;

            var caretBlinkTime = NativeMethods.GetCaretBlinkTime();
            var caretBlinkTimeSpan = new TimeSpan(0, 0, 0, 0, caretBlinkTime);
            _blinkTimer = new DispatcherTimer(
                caretBlinkTimeSpan,
                DispatcherPriority.Normal,
                OnCaretBlinkTimer,
                Dispatcher.CurrentDispatcher);
        }

        internal BlockCaret(IWpfTextView view, string adornmentLayerName, IEditorFormatMap formatMap) :
            this(view, formatMap, view.GetAdornmentLayer(adornmentLayerName))
        {
        }

        private void OnLayoutChanged(object sender, EventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretChanged(object sender, EventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretBlinkTimer(object sender, EventArgs e)
        {
            if (_isShown && _caretData.HasValue)
            {
                var data = _caretData.Value;
                data.Image.Opacity = data.Image.Opacity == 0.0 ? _caretOpacity : 0.0;
            }
        }

        private void DestroyCaret()
        {
            _layer.RemoveAdornmentsByTag(_tag);
            _caretData = null;
        }

        private void MaybeDestroyCaret()
        {
            if (_caretData.HasValue)
            {
                DestroyCaret();
            }
        }

        /// <summary>
        /// Attempt to copy the real caret color
        /// </summary>
        private Color? GetRealCaretBrushColor()
        {
            var properties = _formatMap.GetProperties("Caret");
            var key = "ForegroundColor";
            if (properties.Contains(key))
            {
                return (Color)properties[key];
            }
            else
            {
                return null;
            }
        }

        private Point GetRealCaretVisualPoint()
        {
            return new Point(_view.Caret.Left, _view.Caret.Top);
        }

        private void MoveCaretImageToCaret()
        {
            var point = GetRealCaretVisualPoint();
            var data = _caretData.Value;
            Canvas.SetLeft(data.Image, point.X);
            Canvas.SetTop(data.Image, point.Y);
        }

        private Size GetOptimalCaretSize()
        {
            var caret = _view.Caret;
            var line = caret.ContainingTextViewLine;
            var defaultSize = new Size(5.0, line.IsValid ? line.Height : 10.0);
            if (!IsRealCaretVisible)
            {
                return defaultSize;
            }
            else
            {
                var point = caret.Position.BufferPosition;
                var bounds = line.GetCharacterBounds(point);
                return new Size(bounds.Width, bounds.Height);
            }
        }

        private void CreateCaretData()
        {
            var color = GetRealCaretBrushColor();
            var brush = new SolidColorBrush(color ?? Colors.Black);
            brush.Freeze();

            var pen = new Pen(brush, 1.0);
            var rect = new Rect(GetRealCaretVisualPoint(), GetOptimalCaretSize());
            var geometry = new RectangleGeometry(rect);
            var drawing = new GeometryDrawing(brush, pen, geometry);
            drawing.Freeze();

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            var image = new Image();
            image.Opacity = _caretOpacity;
            image.Source = drawingImage;

            var point = _view.Caret.Position.BufferPosition;
            var data = new CaretData(image, color, point);
            _caretData = data;
            _layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                new SnapshotSpan(point, 0),
                _tag,
                image,
                (x, y) => { _caretData = null; });
            MoveCaretImageToCaret();
        }

        private void UpdateCaret()
        {
            if (_isShown)
            {
                if (!IsRealCaretVisible)
                {
                    MaybeDestroyCaret();
                }
                else if (NeedRecreateCaret)
                {
                    MaybeDestroyCaret();
                    CreateCaretData();
                }
                else
                {
                    MoveCaretImageToCaret();
                }
            }
        }

        public void Hide()
        {
            if (IsShown)
            {
                _isShown = false;
                _blinkTimer.IsEnabled = false;
                _view.Caret.IsHidden = false;
                DestroyCaret();
            }
        }

        public void Show()
        {
            if (!IsShown)
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

        public void Destroy()
        {
            Hide();
            _view.LayoutChanged -= OnLayoutChanged;
            _view.Caret.PositionChanged -= OnCaretChanged;
        }
    }

}
