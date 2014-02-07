using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    internal sealed class BlockCaret : IBlockCaret
    {
        private struct CaretData
        {
            internal readonly CaretDisplay CaretDisplay;
            internal readonly double CaretOpacity;
            internal readonly Image Image;
            internal readonly Color? Color;
            internal readonly Size Size;
            internal readonly double YDisplayOffset;

            internal CaretData(CaretDisplay caretDisplay, double caretOpacity, Image image, Color? color, Size size, double displayOffset)
            {
                CaretDisplay = caretDisplay;
                CaretOpacity = caretOpacity;
                Image = image;
                Color = color;
                Size = size;
                YDisplayOffset = displayOffset;
            }
        }

        private readonly ITextView _textView;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly object _tag = new object();
        private readonly DispatcherTimer _blinkTimer;
        private readonly IControlCharUtil _controlCharUtil;
        private CaretData? _caretData;
        private CaretDisplay _caretDisplay;
        private FormattedText _formattedText;
        private bool _isAdornmentPresent;
        private bool _isDestroyed;
        private bool _isUpdating;
        private double _caretOpacity = 0.65;

        public ITextView TextView
        {
            get { return _textView; }
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

        public FormattedText FormattedText
        {
            get
            {
                if (_formattedText == null)
                {
                    _formattedText = CreateFormattedText();
                }

                return _formattedText;
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
                    var caret = _textView.Caret;
                    var line = caret.ContainingTextViewLine;
                    return line.VisibilityState != VisibilityState.Unattached && _textView.HasAggregateFocus;
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

        internal BlockCaret(ITextView textView, IClassificationFormatMap classificationFormatMap, IEditorFormatMap formatMap, IAdornmentLayer layer, IControlCharUtil controlCharUtil, IProtectedOperations protectedOperations)
        {
            _textView = textView;
            _editorFormatMap = formatMap;
            _adornmentLayer = layer;
            _protectedOperations = protectedOperations;
            _classificationFormatMap = classificationFormatMap;
            _controlCharUtil = controlCharUtil;

            _textView.LayoutChanged += OnCaretEvent;
            _textView.GotAggregateFocus += OnCaretEvent;
            _textView.LostAggregateFocus += OnCaretEvent;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Closed += OnTextViewClosed;

            var caretBlinkTime = GetCaretBlinkTime();
            var caretBlinkTimeSpan = new TimeSpan(0, 0, 0, 0, caretBlinkTime ?? Int32.MaxValue);
            _blinkTimer = new DispatcherTimer(
                caretBlinkTimeSpan,
                DispatcherPriority.Normal,
                _protectedOperations.GetProtectedEventHandler(OnCaretBlinkTimer),
                Dispatcher.CurrentDispatcher);
            _blinkTimer.IsEnabled = caretBlinkTime != null;
        }

        internal BlockCaret(IWpfTextView textView, string adornmentLayerName, IClassificationFormatMap classificationFormatMap, IEditorFormatMap formatMap, IControlCharUtil controlCharUtil, IProtectedOperations protectedOperations) :
            this(textView, classificationFormatMap, formatMap, textView.GetAdornmentLayer(adornmentLayerName), controlCharUtil, protectedOperations)
        {

        }

        /// <summary>
        /// Get the number of milliseconds for the caret blink time.  Null is returned if the 
        /// caret should not blink
        /// </summary>
        private int? GetCaretBlinkTime()
        {
            var blinkTime = NativeMethods.GetCaretBlinkTime();

            // The API returns INFINITE if the caret simply should not blink.  Additionally it returns
            // 0 on error which we will just treat as infinite
            if (blinkTime == NativeMethods.INFINITE || blinkTime == 0)
            {
                return null;
            }

            try
            {
                return checked((int)blinkTime);
            }
            catch (Exception)
            {
                return Int32.MaxValue;
            }
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

        /// <summary>
        /// Whenever the caret moves it should become both visible and reset the blink timer.  This is the
        /// behavior of gVim.  It can be demonstrated by simply moving the caret horizontally along a 
        /// line of text.  If the interval between the movement commands is shorter than the blink timer
        /// the caret will always be visible
        /// </summary>
        private void OnCaretPositionChanged(object sender, EventArgs e)
        {
            if (_blinkTimer.IsEnabled)
            {
                _blinkTimer.IsEnabled = false;
                _blinkTimer.IsEnabled = true;
            }

            // If the caret is invisible, make it visible
            if (_caretData.HasValue && _caretData.Value.CaretDisplay != CaretDisplay.NormalCaret)
            {
                _caretData.Value.Image.Opacity = _caretOpacity;
            }

            UpdateCaret();
        }


        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _blinkTimer.IsEnabled = false;
        }

        private void OnBlockCaretAdornmentRemoved(object sender, UIElement element)
        {
            _isAdornmentPresent = false;
        }

        private void EnsureAdornmentRemoved()
        {
            if (_isAdornmentPresent)
            {
                _adornmentLayer.RemoveAdornmentsByTag(_tag);
                Debug.Assert(!_isAdornmentPresent);
            }
        }

        /// <summary>
        /// Attempt to copy the real caret color
        /// </summary>
        private Color? TryCalculateCaretColor()
        {
            const string key = EditorFormatDefinition.ForegroundColorId;
            var properties = _editorFormatMap.GetProperties(BlockCaretFormatDefinition.Name);
            if (properties.Contains(key))
            {
                return (Color)properties[key];
            }

            return null;
        }

        private Point GetRealCaretVisualPoint()
        {
            return new Point(_textView.Caret.Left, _textView.Caret.Top);
        }

        private void MoveCaretImageToCaret(CaretData caretData)
        {
            var point = GetRealCaretVisualPoint();
            Canvas.SetLeft(caretData.Image, point.X);
            Canvas.SetTop(caretData.Image, point.Y + caretData.YDisplayOffset);
        }

        private FormattedText CreateFormattedText()
        {
            var textRunProperties = _classificationFormatMap.DefaultTextProperties;
            return new FormattedText("^", CultureInfo.CurrentUICulture, FlowDirection.RightToLeft, textRunProperties.Typeface, textRunProperties.FontRenderingEmSize, Brushes.Black);
        }

        /// <summary>
        /// Calculate the dimensions of the caret
        /// </summary>
        private Size CalculateCaretSize()
        {
            double defaultWidth = FormattedText.Width;

            var caret = _textView.Caret;
            var line = caret.ContainingTextViewLine;
            double width = defaultWidth;
            double height = caret.Height;
            if (IsRealCaretVisible)
            {
                // Get the size of the character to which we need to paint the caret.  Special case
                // tab here because it's too big.  When there is a tab we use the default height
                // and width
                var point = caret.Position.BufferPosition;
                if (point.Position >= _textView.TextSnapshot.Length || point.GetChar() == '\t')
                {
                    width = defaultWidth;
                }
                else
                {
                    var bounds = line.GetCharacterBounds(point);
                    if (_controlCharUtil.IsDisplayControlChar(point.GetChar()))
                    {
                        width = defaultWidth;
                    }
                    else
                    {
                        width = bounds.Width;
                        height = bounds.TextHeight;
                    }
                }
            }

            return new Size(width, height);
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
                case CaretDisplay.Select:
                    {
                        var size = new Size(_textView.Caret.Width, _textView.Caret.Height);
                        var point = GetRealCaretVisualPoint();
                        return Tuple.Create(new Rect(point, size), 0d);
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
            return new CaretData(_caretDisplay, _caretOpacity, image, color, rect.Size, tuple.Item2);
        }

        /// <summary>
        /// This determines if the image which is used to represent the caret is stale and needs
        /// to be recreated.  
        /// </summary>
        private bool IsAdornmentStale(CaretData caretData)
        {
            // Size is represented in floating point so strict equality comparison will almost 
            // always return false.  Use a simple epsilon to test the difference

            if ( caretData.Color != TryCalculateCaretColor() ||
                caretData.CaretDisplay != _caretDisplay ||
                caretData.CaretOpacity != _caretOpacity)
            {
                return true;
            }

            var epsilon = 0.001;
            var size = CalculateCaretRectAndDisplayOffset().Item1.Size;
            if (Math.Abs(size.Height - caretData.Size.Height) > epsilon ||
                Math.Abs(size.Width - caretData.Size.Width) > epsilon)
            {
                return true;
            }

            return false;
        }

        private void EnsureCaretDisplayed()
        {
            // For normal caret we just use the standard caret.  Make sure the adornment is removed and 
            // let the normal caret win
            if (CaretDisplay == CaretDisplay.NormalCaret)
            {
                EnsureAdornmentRemoved();
                _textView.Caret.IsHidden = false;
                return;
            }

            _textView.Caret.IsHidden = true;

            if (_caretData == null || IsAdornmentStale(_caretData.Value))
            {
                EnsureAdornmentRemoved();
                _caretData = CreateCaretData();
            }

            var caretData = _caretData.Value;

            MoveCaretImageToCaret(caretData);
            if (!_isAdornmentPresent)
            {
                var caretPoint = _textView.Caret.Position.BufferPosition;
                _isAdornmentPresent = true;
                _adornmentLayer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    new SnapshotSpan(caretPoint, 0),
                    _tag,
                    caretData.Image,
                    OnBlockCaretAdornmentRemoved);
            }
        }

        private void UpdateCaret()
        {
            if (_isUpdating)
            {
                return;
            }

            _isUpdating = true;
            try
            {
                UpdateCaretCore();
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void UpdateCaretCore()
        {
            if (!IsRealCaretVisible)
            {
                EnsureAdornmentRemoved();
            }
            else
            {
                EnsureCaretDisplayed();
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
            EnsureAdornmentRemoved();

            if (!_textView.IsClosed)
            {
                _textView.LayoutChanged -= OnCaretEvent;
                _textView.GotAggregateFocus -= OnCaretEvent;
                _textView.LostAggregateFocus -= OnCaretEvent;
                _textView.Caret.PositionChanged -= OnCaretPositionChanged;
                _textView.Caret.IsHidden = false;
                _textView.Closed -= OnTextViewClosed;
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
