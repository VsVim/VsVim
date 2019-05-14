﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            internal readonly CaretDisplay CaretDisplay;
            internal readonly double CaretOpacity;
            internal readonly UIElement Element;
            internal readonly Color? Color;
            internal readonly Size Size;
            internal readonly double YDisplayOffset;
            internal readonly string CaretCharacter;

            internal CaretData(CaretDisplay caretDisplay, double caretOpacity, UIElement element, Color? color, Size size, double displayOffset, string caretCharacter)
            {
                CaretDisplay = caretDisplay;
                CaretOpacity = caretOpacity;
                Element = element;
                Color = color;
                Size = size;
                YDisplayOffset = displayOffset;
                CaretCharacter = caretCharacter;
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
                    if (!_textView.IsClosed)
                    {
                        var caret = _textView.Caret;
                        var line = caret.ContainingTextViewLine;
                        return line.VisibilityState != VisibilityState.Unattached && _textView.HasAggregateFocus;
                    }
                }
                catch (InvalidOperationException)
                {
                    // InvalidOperationException is thrown when we ask for ContainingTextViewLine and the view
                    // is not yet completely rendered.  It's safe to say at this point that the caret is not 
                    // visible
                }
                return false;
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

            _blinkTimer = CreateBlinkTimer(protectedOperations, OnCaretBlinkTimer);
        }

        internal BlockCaret(IWpfTextView textView, string adornmentLayerName, IClassificationFormatMap classificationFormatMap, IEditorFormatMap formatMap, IControlCharUtil controlCharUtil, IProtectedOperations protectedOperations) :
            this(textView, classificationFormatMap, formatMap, textView.GetAdornmentLayer(adornmentLayerName), controlCharUtil, protectedOperations)
        {
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
            if (_caretData.HasValue && _caretData.Value.CaretDisplay != CaretDisplay.NormalCaret)
            {
                var data = _caretData.Value;
                data.Element.Opacity = data.Element.Opacity == 0.0 ? 1.0 : 0.0;
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
            if (_caretData.HasValue && _caretData.Value.CaretDisplay != CaretDisplay.NormalCaret)
            {
                _caretData.Value.Element.Opacity = _caretOpacity;
            }
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

        private void MoveCaretElementToCaret(CaretData caretData)
        {
            var point = GetRealCaretVisualPoint();
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
        private Size CalculateCaretSize(out string caretCharacter)
        {
            caretCharacter = "";
            var defaultWidth = FormattedText.Width;

            var caret = _textView.Caret;
            var line = caret.ContainingTextViewLine;
            var width = defaultWidth;
            var height = line.TextHeight;

            if (IsRealCaretVisible)
            {
                // Get the size of the character to which we need to paint
                // the caret.  Special case tab here because it's too big.
                // When there is a tab we use the default height and width.
                var point = caret.Position.BufferPosition;
                if (point.Position < _textView.TextSnapshot.Length)
                {
                    var pointCharacter = point.GetChar();
                    if (pointCharacter != '\t'
                        && !_controlCharUtil.IsDisplayControlChar(pointCharacter))
                    {
                        // Handle surrogate pairs.
                        if (Char.IsHighSurrogate(pointCharacter)
                            && point.Position < _textView.TextSnapshot.Length - 1
                            && Char.IsLowSurrogate(point.Add(1).GetChar()))
                        {
                            caretCharacter = new SnapshotSpan(point, 2).GetText();
                        }
                        else
                        {
                            caretCharacter = pointCharacter.ToString();
                        }
                        width = line.GetCharacterBounds(point).Width;
                    }
                }
            }

            return new Size(width, height);
        }

        private Tuple<Rect, double, string> CalculateCaretRectAndDisplayOffset()
        {
            var size = CalculateCaretSize(out string caretCharacter);
            var caretPoint = GetRealCaretVisualPoint();
            var blockPoint = caretPoint;

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
                    size = new Size(_textView.Caret.Width, _textView.Caret.Height);
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
            var offset = blockPoint.Y - caretPoint.Y;
            return Tuple.Create(rect, offset, caretCharacter);
        }

        private CaretData CreateCaretData()
        {
            var color = TryCalculateCaretColor();
            var tuple = CalculateCaretRectAndDisplayOffset();
            var rect = tuple.Item1;
            var width = rect.Size.Width;
            var height = rect.Size.Height;
            var offset = tuple.Item2;
            var caretCharacter = tuple.Item3;

            var properties = _editorFormatMap.GetProperties(BlockCaretFormatDefinition.Name);
            var foregroundBrush = properties.GetForegroundBrush(SystemColors.WindowBrush);
            var backgroundBrush = properties.GetBackgroundBrush(SystemColors.WindowTextBrush);
            var textRunProperties = _classificationFormatMap.DefaultTextProperties;
            var typeface = textRunProperties.Typeface;
            var fontSize = textRunProperties.FontRenderingEmSize;
            var textHeight = offset + height;

            if (_caretOpacity < 1.0 && backgroundBrush is SolidColorBrush solidBrush)
            {
                var alpha = (byte)Math.Round(0xff * _caretOpacity);
                var oldColor = solidBrush.Color;
                var newColor = Color.FromArgb(alpha, oldColor.R, oldColor.G, oldColor.B);
                backgroundBrush = new SolidColorBrush(newColor);
            }

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
                LineHeight = textHeight != 0 ? textHeight : double.NaN,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                BaselineOffset = 0,
            };

            var element = new Canvas
            {
                Width = width,
                Height = height,
                ClipToBounds = true,
            };

            element.Children.Add(textBlock);
            Canvas.SetTop(textBlock, -offset);
            Canvas.SetLeft(textBlock, 0);

            return new CaretData(_caretDisplay, _caretOpacity, element, color, rect.Size, offset, caretCharacter);
        }

        /// <summary>
        /// This determines if the image which is used to represent the caret is stale and needs
        /// to be recreated.  
        /// </summary>
        private bool IsAdornmentStale(CaretData caretData)
        {
            // Size is represented in floating point so strict equality comparison will almost 
            // always return false.  Use a simple epsilon to test the difference

            if (caretData.Color != TryCalculateCaretColor() ||
                caretData.CaretDisplay != _caretDisplay ||
                caretData.CaretOpacity != _caretOpacity)
            {
                return true;
            }

            var tuple = CalculateCaretRectAndDisplayOffset();

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

            MoveCaretElementToCaret(caretData);
            if (!_isAdornmentPresent)
            {
                var caretPoint = _textView.Caret.Position.BufferPosition;
                _isAdornmentPresent = true;
                _adornmentLayer.AddAdornment(
                    AdornmentPositioningBehavior.TextRelative,
                    new SnapshotSpan(caretPoint, 0),
                    _tag,
                    caretData.Element,
                    OnBlockCaretAdornmentRemoved);
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
