using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf.Implementation.BlockCaret
{
    internal sealed class BlockCaret : IBlockCaret
    {
        private readonly struct CaretData
        {
            internal readonly int CaretIndex;
            internal readonly CaretDisplay CaretDisplay;
            internal readonly double CaretOpacity;
            internal readonly UIElement Element;
            internal readonly Color? Color;
            internal readonly Size Size;
            internal readonly double YDisplayOffset;
            internal readonly double BaselineOffset;
            internal readonly string CaretCharacter;

            internal CaretData(
                int caretIndex,
                CaretDisplay caretDisplay,
                double caretOpacity,
                UIElement element,
                Color? color,
                Size size,
                double displayOffset,
                double baselineOffset,
                string caretCharacter)
            {
                CaretIndex = caretIndex;
                CaretDisplay = caretDisplay;
                CaretOpacity = caretOpacity;
                Element = element;
                Color = color;
                Size = size;
                YDisplayOffset = displayOffset;
                BaselineOffset = baselineOffset;
                CaretCharacter = caretCharacter;
            }
        }

        private readonly IVimHost _vimHost;
        private readonly ITextView _textView;
        private readonly IProtectedOperations _protectedOperations;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly IAdornmentLayer _adornmentLayer;
        private readonly List<object> _tags = new List<object>();
        private readonly HashSet<object> _tagsInUse = new HashSet<object>();
        private readonly DispatcherTimer _blinkTimer;
        private readonly IControlCharUtil _controlCharUtil;

        private List<VirtualSnapshotPoint> _caretPoints = new List<VirtualSnapshotPoint>();
        private Dictionary<int, CaretData> _caretDataMap = new Dictionary<int, CaretData>();
        private CaretDisplay _caretDisplay;
        private FormattedText _formattedText;
        private bool _isDestroyed;
        private bool _isUpdating;
        private double _caretOpacity = 1.0;

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

        private ITextViewLine GetTextViewLineContainingPoint(VirtualSnapshotPoint caretPoint)
        {
            try
            {
                if (!_textView.IsClosed && !_textView.InLayout)
                {
                    var textViewLines = _textView.TextViewLines;
                    if (textViewLines != null && textViewLines.IsValid)
                    {
                        var line = textViewLines.GetTextViewLineContainingBufferPosition(caretPoint.Position);
                        if (line != null && line.IsValid)
                        {
                            return line;
                        }

                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                VimTrace.TraceError(ex);
            }
            return null;
        }

        /// <summary>
        /// Is the real caret visible in some way?
        /// </summary>
        private bool IsRealCaretVisible(VirtualSnapshotPoint caretPoint)
        {
            if (_textView.HasAggregateFocus)
            {
                var line = GetTextViewLineContainingPoint(caretPoint);
                return line != null && line.VisibilityState != VisibilityState.Unattached;
            }
            return false;
        }

        internal BlockCaret(
            IVimHost vimHost,
            ITextView textView,
            IClassificationFormatMap classificationFormatMap,
            IEditorFormatMap formatMap,
            IAdornmentLayer layer,
            IControlCharUtil controlCharUtil,
            IProtectedOperations protectedOperations)
        {
            _vimHost = vimHost;
            _textView = textView;
            _editorFormatMap = formatMap;
            _adornmentLayer = layer;
            _protectedOperations = protectedOperations;
            _classificationFormatMap = classificationFormatMap;
            _controlCharUtil = controlCharUtil;

            _textView.LayoutChanged += OnCaretEvent;
            _textView.GotAggregateFocus += OnCaretEvent;
            _textView.LostAggregateFocus += OnCaretEvent;
            _textView.Selection.SelectionChanged += OnCaretEvent;
            _textView.Caret.PositionChanged += OnCaretPositionChanged;
            _textView.Closed += OnTextViewClosed;

            _blinkTimer = CreateBlinkTimer(protectedOperations, OnCaretBlinkTimer);
        }

        internal BlockCaret(
            IVimHost vimHost,
            IWpfTextView textView,
            string adornmentLayerName,
            IClassificationFormatMap classificationFormatMap,
            IEditorFormatMap formatMap,
            IControlCharUtil controlCharUtil,
            IProtectedOperations protectedOperations) :
            this(
                vimHost,
                textView,
                classificationFormatMap,
                formatMap,
                textView.GetAdornmentLayer(adornmentLayerName),
                controlCharUtil,
                protectedOperations)
        {
        }

        /// <summary>
        /// Snap the specifed value to whole device pixels, optionally ensuring
        /// that the value is positive
        /// </summary>
        /// <param name="value"></param>
        /// <param name="ensurePositive"></param>
        /// <returns></returns>
        private double SnapToWholeDevicePixels(double value, bool ensurePositive)
        {
            if (_textView is IWpfTextView wpfTextView)
            {
                var visualElement = wpfTextView.VisualElement;
                var presentationSource = PresentationSource.FromVisual(visualElement);
                var matrix = presentationSource.CompositionTarget.TransformToDevice;
                var dpiFactor = 1.0 / matrix.M11;
                var wholePixels = Math.Round(value / dpiFactor);
                if (ensurePositive && wholePixels < 1.0)
                {
                    wholePixels = 1.0;
                }
                return wholePixels * dpiFactor;
            }
            return value;
        }

        /// <summary>
        /// Get the number of milliseconds for the caret blink time.  Null is returned if the 
        /// caret should not blink
        /// </summary>
        private static int? GetCaretBlinkTime()
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
                return null;
            }
        }

        /// <summary>
        /// This helper is used to work around a reported, but unreproducable, bug. The constructor
        /// of DispatcherTimer is throwing an exception claiming a millisecond time greater 
        /// than int.MaxValue is being passed to the constructor.
        /// 
        /// This is clearly not possible given the input is an int value.  However after multiple user
        /// reports it's clear the exception is getting triggered.
        ///
        /// The only semi-plausible idea I can come up with is a floating point conversion issue.  Given
        /// that the input to Timespan is int and the compared value is double it's possible that a 
        /// conversion / rounding issue is causing int.MaxValue to become int.MaxValue + 1.  
        ///
        /// Either way though need to guard against this case to unblock users.
        /// 
        /// https://github.com/VsVim/VsVim/issues/631
        /// https://github.com/VsVim/VsVim/issues/1860
        /// </summary>
        private static DispatcherTimer CreateBlinkTimer(IProtectedOperations protectedOperations, EventHandler onCaretBlinkTimer)
        {
            var caretBlinkTime = GetCaretBlinkTime();
            var caretBlinkTimeSpan = new TimeSpan(0, 0, 0, 0, caretBlinkTime ?? int.MaxValue);
            try
            {
                var blinkTimer = new DispatcherTimer(
                    caretBlinkTimeSpan,
                    DispatcherPriority.Normal,
                    protectedOperations.GetProtectedEventHandler(onCaretBlinkTimer),
                    Dispatcher.CurrentDispatcher)
                {
                    IsEnabled = caretBlinkTime != null
                };
                return blinkTimer;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Hit the bug ... just create a simple timer with a default interval.
                VimTrace.TraceError("Error creating BlockCaret DispatcherTimer");
                var blinkTimer = new DispatcherTimer(
                    TimeSpan.FromSeconds(2),
                    DispatcherPriority.Normal,
                    protectedOperations.GetProtectedEventHandler(onCaretBlinkTimer),
                    Dispatcher.CurrentDispatcher)
                {
                    IsEnabled = true
                };
                return blinkTimer;
            }
        }

        private void OnCaretEvent(object sender, EventArgs e)
        {
            UpdateCaret();
        }

        private void OnCaretBlinkTimer(object sender, EventArgs e)
        {
            foreach (var caretData in _caretDataMap.Values)
            {
                if (caretData.CaretDisplay != CaretDisplay.NormalCaret)
                {
                    caretData.Element.Opacity = caretData.Element.Opacity == 0.0 ? 1.0 : 0.0;
                }
            }
        }

        /// <summary>
        /// Whenever the caret moves it should become both visible and reset the blink timer.  This is the
        /// behavior of gVim.  It can be demonstrated by simply moving the caret horizontally along a 
        /// line of text.  If the interval between the movement commands is shorter than the blink timer
        /// the caret will always be visible
        /// </summary>
        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            RestartBlinkCycle();

            UpdateCaret();
        }

        private void RestartBlinkCycle()
        {
            if (_blinkTimer.IsEnabled)
            {
                _blinkTimer.IsEnabled = false;
                _blinkTimer.IsEnabled = true;
            }

            // If the caret is invisible, make it visible
            foreach (var caretData in _caretDataMap.Values)
            {
                if (caretData.CaretDisplay != CaretDisplay.NormalCaret)
                {
                    caretData.Element.Opacity = _caretOpacity;
                }
            }
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            _blinkTimer.IsEnabled = false;
        }

        private void OnBlockCaretAdornmentRemoved(object tag, UIElement element)
        {
            _tagsInUse.Remove(tag);
        }

        private void EnsureAdnormentsRemoved()
        {
            while (_tagsInUse.Count > 0)
            {
                var tag = _tagsInUse.First();
                EnsureAdnormentRemoved(tag);
            }
        }

        private void EnsureAdnormentRemoved(object tag)
        {
            _adornmentLayer.RemoveAdornmentsByTag(tag);
            Contract.Assert(!_tagsInUse.Contains(tag));
        }

        private string GetFormatName(int caretIndex, int numberOfCarets)
        {
            return
                numberOfCarets == 1
                ? BlockCaretFormatDefinition.Name
                : (caretIndex == 0
                    ? PrimaryCaretFormatDefinition.Name
                    : SecondaryCaretFormatDefinition.Name);
        }
        
        /// <summary>
        /// Attempt to copy the real caret color
        /// </summary>
        private Color? TryCalculateCaretColor(int caretIndex, int numberOfCarets)
        {
            var formatName = GetFormatName(caretIndex, numberOfCarets);
            const string key = EditorFormatDefinition.BackgroundColorId;
            var properties = _editorFormatMap.GetProperties(formatName);
            if (properties.Contains(key))
            {
                return (Color)properties[key];
            }

            return null;
        }

        private Point GetRealCaretVisualPoint(VirtualSnapshotPoint caretPoint)
        {
            // Default screen position is the same as that of the native caret.
            var textViewLine = GetTextViewLineContainingPoint(caretPoint);
            var bounds = textViewLine.GetCharacterBounds(caretPoint);
            var left = bounds.Left;
            var top = bounds.Top;

            if (_caretDisplay == CaretDisplay.Block ||
                _caretDisplay == CaretDisplay.HalfBlock ||
                _caretDisplay == CaretDisplay.QuarterBlock)
            {
                var point = caretPoint.Position;
                if (point < _textView.TextSnapshot.Length && point.GetChar() == '\t')
                {
                    // Any kind of block caret situated on a tab floats over
                    // the last space occupied by the tab.
                    var width = textViewLine.GetCharacterBounds(point).Width;
                    var defaultWidth = _formattedText.Width;
                    var offset = Math.Max(0, width - defaultWidth);
                    left += offset;
                }
            }

            return new Point(left, top);
        }

        private void MoveCaretElementToCaret(VirtualSnapshotPoint caretPoint, CaretData caretData)
        {
            var point = GetRealCaretVisualPoint(caretPoint);
            if (caretData.CaretDisplay == CaretDisplay.Select)
            {
                point = new Point(SnapToWholeDevicePixels(point.X, ensurePositive: false), point.Y);
            }
            Canvas.SetLeft(caretData.Element, point.X);
            Canvas.SetTop(caretData.Element, point.Y + caretData.YDisplayOffset);
        }

        private FormattedText CreateFormattedText()
        {
            var textRunProperties = _classificationFormatMap.DefaultTextProperties;
            return new FormattedText("A", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, textRunProperties.Typeface, textRunProperties.FontRenderingEmSize, Brushes.Black);
        }

        /// <summary>
        /// Calculate the dimensions of the caret
        /// </summary>
        private Size CalculateCaretSize(VirtualSnapshotPoint caretPoint, out string caretCharacter)
        {
            caretCharacter = "";

            var defaultWidth = _formattedText.Width;
            var width = defaultWidth;
            var height = _textView.LineHeight;

            if (IsRealCaretVisible(caretPoint))
            {
                // Get the caret string and caret width.
                var line = GetTextViewLineContainingPoint(caretPoint);
                height = line.Height;
                var point = caretPoint.Position;
                var codePointInfo = new SnapshotCodePoint(point).CodePointInfo;
                if (point.Position < point.Snapshot.Length)
                {
                    var pointCharacter = point.GetChar();
                    if (_controlCharUtil.TryGetDisplayText(pointCharacter, out string caretString))
                    {
                        // Handle control character notation.
                        caretCharacter = caretString;
                        width = line.GetCharacterBounds(point).Width;
                    }
                    else if (codePointInfo == CodePointInfo.SurrogatePairHighCharacter)
                    {
                        // Handle surrogate pairs.
                        caretCharacter = new SnapshotSpan(point, 2).GetText();
                        width = line.GetCharacterBounds(point).Width;
                    }
                    else if (pointCharacter == '\t')
                    {
                        // Handle tab as no character and default width,
                        // except no wider than the tab's screen width.
                        caretCharacter = "";
                        width = Math.Min(defaultWidth, line.GetCharacterBounds(point).Width);
                    }
                    else if (pointCharacter == '\r' || pointCharacter == '\n')
                    {
                        // Handle linebreak.
                        caretCharacter = "";
                        width = line.GetCharacterBounds(point).Width;
                    }
                    else
                    {
                        // Handle ordinary UTF16 character.
                        caretCharacter = pointCharacter.ToString();
                        width = line.GetCharacterBounds(point).Width;
                    }
                }
            }

            return new Size(width, height);
        }

        private double CalculateBaselineOffset(VirtualSnapshotPoint caretPoint)
        {
            var offset = 0.0;
            if (IsRealCaretVisible(caretPoint))
            {
                var line = GetTextViewLineContainingPoint(caretPoint);
                offset = Math.Max(0.0, line.Baseline - _formattedText.Baseline);
            }
            return offset;
        }

        private Tuple<Rect, double, string> CalculateCaretRectAndDisplayOffset(VirtualSnapshotPoint caretPoint)
        {
            var size = CalculateCaretSize(caretPoint, out string caretCharacter);
            var point = GetRealCaretVisualPoint(caretPoint);
            var blockPoint = point;

            switch (_caretDisplay)
            {
                case CaretDisplay.Block:
                    break;

                case CaretDisplay.HalfBlock:
                    size = new Size(size.Width, size.Height / 2);
                    blockPoint = new Point(blockPoint.X, blockPoint.Y + size.Height);
                    break;

                case CaretDisplay.QuarterBlock:
                    size = new Size(size.Width, size.Height / 4);
                    blockPoint = new Point(blockPoint.X, blockPoint.Y + 3 * size.Height);
                    break;

                case CaretDisplay.Select:
                    caretCharacter = null;
                    var width = SnapToWholeDevicePixels(_textView.Caret.Width, ensurePositive: true);
                    var height = _textView.Caret.Height;

                    size = new Size(width, height);
                    break;

                case CaretDisplay.Invisible:
                case CaretDisplay.NormalCaret:
                    caretCharacter = null;
                    size = new Size(0, 0);
                    break;

                default:
                    throw new InvalidOperationException("Invalid enum value");
            }
            var rect = new Rect(blockPoint, size);
            var offset = blockPoint.Y - point.Y;
            return Tuple.Create(rect, offset, caretCharacter);
        }

        private CaretData CreateCaretData(int caretIndex, int numberOfCarets)
        {
            var caretPoint = _caretPoints[caretIndex];
            _formattedText = CreateFormattedText();
            var color = TryCalculateCaretColor(caretIndex, numberOfCarets);
            var tuple = CalculateCaretRectAndDisplayOffset(caretPoint);
            var baselineOffset = CalculateBaselineOffset(caretPoint);
            var rect = tuple.Item1;
            var width = rect.Size.Width;
            var height = rect.Size.Height;
            var offset = tuple.Item2;
            var caretCharacter = tuple.Item3;

            var formatName = GetFormatName(caretIndex, numberOfCarets);
            var properties = _editorFormatMap.GetProperties(formatName);
            var foregroundBrush = properties.GetForegroundBrush(SystemColors.WindowBrush);
            var backgroundBrush = properties.GetBackgroundBrush(SystemColors.WindowTextBrush);
            var textRunProperties = _classificationFormatMap.DefaultTextProperties;
            var typeface = textRunProperties.Typeface;
            var fontSize = textRunProperties.FontRenderingEmSize;
            var textHeight = offset + height;
            var lineHeight = _textView.LineHeight;

            if (_caretOpacity < 1.0 && backgroundBrush is SolidColorBrush solidBrush)
            {
                var alpha = (byte)Math.Round(0xff * _caretOpacity);
                var oldColor = solidBrush.Color;
                var newColor = Color.FromArgb(alpha, oldColor.R, oldColor.G, oldColor.B);
                backgroundBrush = new SolidColorBrush(newColor);
            }

            var rectangle = new Rectangle
            {
                Width = width,
                Height = baselineOffset,
                Fill = backgroundBrush,
            };

            var textBlock = new TextBlock
            {
                Text = caretCharacter,
                Foreground = foregroundBrush,
                Background = backgroundBrush,
                FontFamily = typeface.FontFamily,
                FontStretch = typeface.Stretch,
                FontWeight = typeface.Weight,
                FontStyle = typeface.Style,
                FontSize = fontSize,
                Width = width,
                Height = textHeight,
                LineHeight = lineHeight,
            };

            var element = new Canvas
            {
                Width = width,
                Height = height,
                ClipToBounds = true,
                Children =
                {
                    rectangle,
                    textBlock,
                },
            };

            Canvas.SetTop(rectangle, -offset);
            Canvas.SetLeft(textBlock, 0);

            Canvas.SetTop(textBlock, -offset + baselineOffset);
            Canvas.SetLeft(textBlock, 0);

            return new CaretData(
                caretIndex,
                _caretDisplay,
                _caretOpacity,
                element,
                color,
                rect.Size,
                offset,
                baselineOffset,
                caretCharacter);
        }

        /// <summary>
        /// This determines if the image which is used to represent the caret is stale and needs
        /// to be recreated.  
        /// </summary>
        private bool IsAdornmentStale(VirtualSnapshotPoint caretPoint, CaretData caretData, int numberOfCarets)
        {
            // Size is represented in floating point so strict equality comparison will almost 
            // always return false.  Use a simple epsilon to test the difference

            if (caretData.Color != TryCalculateCaretColor(caretData.CaretIndex, numberOfCarets)
                || caretData.CaretDisplay != _caretDisplay
                || caretData.CaretOpacity != _caretOpacity)
            {
                return true;
            }

            var tuple = CalculateCaretRectAndDisplayOffset(caretPoint);

            var epsilon = 0.001;
            var size = tuple.Item1.Size;
            if (Math.Abs(size.Height - caretData.Size.Height) > epsilon ||
                Math.Abs(size.Width - caretData.Size.Width) > epsilon)
            {
                return true;
            }

            var caretCharacter = tuple.Item3;
            if (caretData.CaretCharacter != caretCharacter)
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
                EnsureAdnormentsRemoved();
                _textView.Caret.IsHidden = false;
                return;
            }

            _textView.Caret.IsHidden = true;

            var numberOfCarets = _caretPoints.Count;
            for (var caretIndex = 0; caretIndex < numberOfCarets; caretIndex++)
            {
                if (caretIndex == _tags.Count)
                {
                    _tags.Add(new object());
                }
                var tag = _tags[caretIndex];

                var caretPoint = _caretPoints[caretIndex];
                if (_caretDataMap.TryGetValue(caretIndex, out CaretData value))
                {
                    if (IsAdornmentStale(caretPoint, value, numberOfCarets))
                    {
                        EnsureAdnormentRemoved(tag);
                        _caretDataMap[caretIndex] = CreateCaretData(caretIndex, numberOfCarets);
                    }
                }
                else
                {
                    _caretDataMap[caretIndex] = CreateCaretData(caretIndex, numberOfCarets);
                }

                var caretData = _caretDataMap[caretIndex];

                MoveCaretElementToCaret(caretPoint, caretData);
                if (!_tagsInUse.Contains(tag))
                {
                    _tagsInUse.Add(tag);
                    _adornmentLayer.AddAdornment(
                        AdornmentPositioningBehavior.TextRelative,
                        new SnapshotSpan(caretPoint.Position, 0),
                        tag,
                        caretData.Element,
                        OnBlockCaretAdornmentRemoved);
                }
            }
            while (_caretDataMap.Count > numberOfCarets)
            {
                var caretIndex = _caretDataMap.Count - 1;
                var caretData = _caretDataMap[caretIndex];
                EnsureAdnormentRemoved(_tags[caretIndex]);
                _caretDataMap.Remove(caretIndex);
            }

            // When the caret display is changed (e.g. from normal to block) we
            // need to restart the blink cycle so that the caret is immediately
            // visible.  Reported in issue #2301.
            RestartBlinkCycle();
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
            _caretPoints =
                _vimHost.GetSelectedSpans(_textView)
                .Select(span => span.CaretPoint)
                .ToList();

            if (_caretPoints.Count == 0 || !IsRealCaretVisible(_caretPoints[0]))
            {
                EnsureAdnormentsRemoved();
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
            EnsureAdnormentsRemoved();

            if (!_textView.IsClosed)
            {
                _textView.LayoutChanged -= OnCaretEvent;
                _textView.GotAggregateFocus -= OnCaretEvent;
                _textView.LostAggregateFocus -= OnCaretEvent;
                _textView.Selection.SelectionChanged -= OnCaretEvent;
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
