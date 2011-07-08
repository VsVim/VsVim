using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.Implementation
{
    internal sealed class BlockCaret : IBlockCaret
    {
        private struct CaretData
        {
            internal readonly CaretDisplay CaretDisplay;
            internal readonly double CaretOpacity;
            internal readonly Image Image;
            internal readonly Color? Color;
            internal readonly SnapshotPoint Point;
            internal readonly double YDisplayOffset;

            internal CaretData(CaretDisplay caretDisplay, double caretOpacity, Image image, Color? color, SnapshotPoint point, double displayOffset)
            {
                CaretDisplay = caretDisplay;
                CaretOpacity = caretOpacity;
                Image = image;
                Color = color;
                Point = point;
                YDisplayOffset = displayOffset;
            }
        }

        private readonly ITextView _view;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IEditorFormatMap _formatMap;
        private readonly IAdornmentLayer _layer;
        private readonly Object _tag = new object();
        private readonly DispatcherTimer _blinkTimer;
        private CaretData? _caretData;
        private CaretDisplay _caretDisplay;
        private bool _isDestroyed;
        private double _caretOpacity = 0.65;

        public ITextView TextView
        {
            get { return _view; }
        }

        public CaretDisplay CaretDisplay
        {
            get { return _caretDisplay; }
            set
            {
                if (_caretDisplay != value)
                {
                    _caretDisplay = value;
                    UpdateCaret();
                }
            }
        }

        public double CaretOpacity
        {
            get { return _caretOpacity; }
            set
            {
                if (_caretOpacity != value)
                {
                    _caretOpacity = value;
                    UpdateCaret();
                }
            }
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
                    return line.VisibilityState != VisibilityState.Unattached && _view.HasAggregateFocus;
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
                    return data.Color != TryCalculateCaretColor()
                        || data.Point != _view.Caret.Position.BufferPosition
                        || data.CaretDisplay != _caretDisplay
                        || data.CaretOpacity != _caretOpacity;
                }

                return true;
            }
        }

        internal BlockCaret(ITextView view, IEditorFormatMap formatMap, IAdornmentLayer layer, IProtectedOperations protectedOperations)
        {
            _view = view;
            _formatMap = formatMap;
            _layer = layer;
            _protectedOperations = protectedOperations;

            _view.LayoutChanged += OnCaretEvent;
            _view.GotAggregateFocus += OnCaretEvent;
            _view.LostAggregateFocus += OnCaretEvent;
            _view.Caret.PositionChanged += OnCaretEvent;
            _view.Closed += OnTextViewClosed;

            var caretBlinkTime = NativeMethods.GetCaretBlinkTime();
            var caretBlinkTimeSpan = new TimeSpan(0, 0, 0, 0, caretBlinkTime);
            _blinkTimer = new DispatcherTimer(
                caretBlinkTimeSpan,
                DispatcherPriority.Normal,
                _protectedOperations.GetProtectedEventHandler(OnCaretBlinkTimer),
                Dispatcher.CurrentDispatcher);
        }

        internal BlockCaret(IWpfTextView view, string adornmentLayerName, IEditorFormatMap formatMap, IProtectedOperations protectedOperations) :
            this(view, formatMap, view.GetAdornmentLayer(adornmentLayerName), protectedOperations)
        {
        }

        private void OnCaretEvent(object sender, EventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretBlinkTimer(object sender, EventArgs e)
        {
            if (_caretData.HasValue && _caretData.Value.CaretDisplay != CaretDisplay.NormalCaret)
            {
                var data = _caretData.Value;
                data.Image.Opacity = data.Image.Opacity == 0.0 ? _caretOpacity : 0.0;
            }
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _blinkTimer.IsEnabled = false;
        }

        private void DestroyBlockCaretDisplay()
        {
            _layer.RemoveAdornmentsByTag(_tag);
            _caretData = null;
        }

        private void MaybeDestroyBlockCaretDisplay()
        {
            if (_caretData.HasValue)
            {
                DestroyBlockCaretDisplay();
            }
        }

        /// <summary>
        /// Attempt to copy the real caret color
        /// </summary>
        private Color? TryCalculateCaretColor()
        {
            const string key = EditorFormatDefinition.ForegroundColorId;
            var properties = _formatMap.GetProperties(EditorFormatDefinitionNames.BlockCaret);
            if (properties.Contains(key))
            {
                return (Color)properties[key];
            }

            return null;
        }

        private Point GetRealCaretVisualPoint()
        {
            return new Point(_view.Caret.Left, _view.Caret.Top);
        }

        private void MoveCaretImageToCaret()
        {
            var point = GetRealCaretVisualPoint();
            if (_caretData.HasValue)
            {
                var data = _caretData.Value;
                Canvas.SetLeft(data.Image, point.X);
                Canvas.SetTop(data.Image, point.Y + data.YDisplayOffset);
            }
        }

        /// <summary>
        /// Calculate the dimensions of the caret
        /// </summary>
        private Size CalculateCaretSize()
        {
            const double defaultWidth = 5.0;
            const double defaultHeight = 10.0;

            var caret = _view.Caret;
            var line = caret.ContainingTextViewLine;
            Size caretSize;
            if (IsRealCaretVisible)
            {
                // Get the size of the character to which we need to paint the caret.  Special case
                // tab here because it's too big.  When there is a tab we use the default height
                // and width
                var point = caret.Position.BufferPosition;
                if (point.Position < _view.TextSnapshot.Length && point.GetChar() != '\t')
                {
                    var bounds = line.GetCharacterBounds(point);
                    caretSize = new Size(bounds.Width, bounds.Height);
                }
                else
                {
                    caretSize = new Size(defaultWidth, line.IsValid ? line.Height : defaultHeight);
                }
            }
            else
            {
                caretSize = new Size(defaultWidth, line.IsValid ? line.Height : defaultHeight);
            }

            return caretSize;
        }

        private Tuple<Rect, double> CalculateCaretRectAndDisplayOffset()
        {
            switch (_caretDisplay)
            {
                case CaretDisplay.Block:
                    return Tuple.Create(new Rect(GetRealCaretVisualPoint(), CalculateCaretSize()), 0d);
                case CaretDisplay.HalfBlock:
                    {
                        var size = CalculateCaretSize();
                        size = new Size(size.Width, size.Height / 2);

                        var point = GetRealCaretVisualPoint();
                        point = new Point(point.X, point.Y + size.Height);
                        return Tuple.Create(new Rect(point, size), size.Height);
                    }
                case CaretDisplay.QuarterBlock:
                    {
                        var size = CalculateCaretSize();
                        var quarter = size.Height / 4;
                        size = new Size(size.Width, quarter);

                        var point = GetRealCaretVisualPoint();
                        var offset = quarter * 3;
                        point = new Point(point.X, point.Y + offset);
                        return Tuple.Create(new Rect(point, size), offset);
                    }
                case CaretDisplay.Invisible:
                case CaretDisplay.NormalCaret:
                    return Tuple.Create(new Rect(GetRealCaretVisualPoint(), new Size(0, 0)), 0d);

                default:
                    throw new InvalidOperationException("Invalid enum value");
            }
        }

        private CaretData CreateCaretData()
        {
            var color = TryCalculateCaretColor();
            var brush = new SolidColorBrush(color ?? Colors.Black);
            brush.Freeze();

            var pen = new Pen(brush, 1.0);
            var tuple = CalculateCaretRectAndDisplayOffset();
            var rect = tuple.Item1;
            var geometry = new RectangleGeometry(rect);
            var drawing = new GeometryDrawing(brush, pen, geometry);
            drawing.Freeze();

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            var image = new Image { Opacity = _caretOpacity, Source = drawingImage };
            var point = _view.Caret.Position.BufferPosition;
            return new CaretData(_caretDisplay, _caretOpacity, image, color, point, tuple.Item2);
        }

        private void CreateBlockCaretDisplay()
        {
            var data = CreateCaretData();
            _caretData = data;
            _layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                new SnapshotSpan(data.Point, 0),
                _tag,
                data.Image,
                (x, y) => { _caretData = null; });

            if (_caretDisplay != CaretDisplay.NormalCaret)
            {
                _view.Caret.IsHidden = true;
                MoveCaretImageToCaret();

                // Restart the timer so the block caret doesn't immediately disappear
                _blinkTimer.IsEnabled = false;
                _blinkTimer.IsEnabled = true;
            }
            else
            {
                _view.Caret.IsHidden = false;
            }
        }

        private void UpdateCaret()
        {
            if (!IsRealCaretVisible)
            {
                MaybeDestroyBlockCaretDisplay();
            }
            else if (NeedRecreateCaret)
            {
                MaybeDestroyBlockCaretDisplay();
                CreateBlockCaretDisplay();
            }
            else
            {
                MoveCaretImageToCaret();
            }
        }

        /// <summary>
        /// Destroy all of the caret related data such that the caret is free for collection.  In particular
        /// make sure to disable the DispatchTimer as keeping it alive will prevent collection
        /// </summary>
        private void DestroyCore()
        {
            _isDestroyed = true;
            _blinkTimer.IsEnabled = false;
            MaybeDestroyBlockCaretDisplay();

            if (!_view.IsClosed)
            {
                _view.LayoutChanged -= OnCaretEvent;
                _view.GotAggregateFocus -= OnCaretEvent;
                _view.LostAggregateFocus -= OnCaretEvent;
                _view.Caret.PositionChanged -= OnCaretEvent;
                _view.Caret.IsHidden = false;
                _view.Closed -= OnTextViewClosed;
            }
        }

        private void MaybeDestroy()
        {
            if (!_isDestroyed)
            {
                DestroyCore();
            }
        }

        public void Destroy()
        {
            MaybeDestroy();
        }
    }

}
