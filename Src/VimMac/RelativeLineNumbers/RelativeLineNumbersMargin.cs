using System;
using System.Collections.Generic;
using System.Diagnostics;
using AppKit;
using CoreAnimation;
using CoreGraphics;
using CoreText;
using Foundation;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers;
using Vim.UI.Wpf.Implementation.RelativeLineNumbers.Util;

namespace Vim.UI.Cocoa.Implementation.RelativeLineNumbers
{
    internal sealed class RelativeLineNumbersMargin : NSView, ICocoaTextViewMargin
    {
        // Large enough that we shouldn't expect the view to reallocate the array.
        private const int ExpectedNumberOfVisibleLines = 100;

        #region Private Members

        private readonly List<CocoaLineNumberMarginDrawingVisual> recyclableVisuals
            = new List<CocoaLineNumberMarginDrawingVisual>(ExpectedNumberOfVisibleLines);

        private readonly Queue<int> unusedChildIndicies = new Queue<int>(ExpectedNumberOfVisibleLines);

        private ICocoaTextView _textView;
        private ICocoaClassificationFormatMap _classificationFormatMap;
        private IClassificationTypeRegistryService _classificationTypeRegistry;
        internal NSStringAttributes _formatting;
        private NSStringAttributes _currentLineFormatting;

        private int _visibleDigits = 5;

        internal bool _updateNeeded = false;
        private bool _isDisposed = false;

        private NSView _translatedCanvas;

        private double _oldViewportTop;
        private LineNumbersCalculator _lineNumbersCalculator;
        private IVimLocalSettings _localSettings;
        private int lastLineNumber;

        #endregion // Private Members

        /// <summary>
        /// Creates a default Line Number Provider for a Text Editor
        /// </summary>
        /// <param name="textView">
        /// The Text Editor with which this line number provider is associated
        /// </param>
        /// <param name="classificationFormatMap">Used for getting/setting the format of the line number classification</param>
        /// <param name="classificationTypeRegistry">Used for retrieving the "line number" classification</param>
        public RelativeLineNumbersMargin(
            ICocoaTextView textView,
            ICocoaClassificationFormatMap classificationFormatMap,
            IClassificationTypeRegistryService classificationTypeRegistry,
            IVimLocalSettings vimLocalSettings)
        {
            _textView = textView;
            _classificationFormatMap = classificationFormatMap;
            _classificationTypeRegistry = classificationTypeRegistry;
            _translatedCanvas = this;
            _oldViewportTop = 0.0;

            _lineNumbersCalculator = new LineNumbersCalculator(textView, vimLocalSettings);
            _localSettings = vimLocalSettings;
            WantsLayer = true;
            Hidden = false;
        }

        public override bool IsFlipped => true;

        public override bool Hidden
        {
            get => base.Hidden;
            set
            {
                base.Hidden = value;

                if (!value)
                {
                    // Sign up for layout changes on the Text Editor
                    _textView.LayoutChanged += OnEditorLayoutChanged;

                    // Sign up for classification format change events
                    _classificationFormatMap.ClassificationFormatMappingChanged += OnClassificationFormatChanged;

                    _textView.ZoomLevelChanged += _textView_ZoomLevelChanged;
                    _textView.Caret.PositionChanged += Caret_PositionChanged;
                    //Fonts might have changed while we were hidden.
                    this.SetFontFromClassification();
                }
                else
                {
                    // Unregister from layout changes on the Text Editor
                    _textView.LayoutChanged -= OnEditorLayoutChanged;
                    _textView.ZoomLevelChanged -= _textView_ZoomLevelChanged;
                    _textView.Caret.PositionChanged -= Caret_PositionChanged;
                    // Unregister from classification format change events
                    _classificationFormatMap.ClassificationFormatMappingChanged -= OnClassificationFormatChanged;
                }
            }
        }

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            var lineNumber = e.TextView.Caret.ContainingTextViewLine.GetLineNumber();
            if (lineNumber != lastLineNumber)
            {
                lastLineNumber = lineNumber;
                _updateNeeded = true;
                UpdateLineNumbers();
            }
        }

        private void _textView_ZoomLevelChanged(object sender, ZoomLevelChangedEventArgs e)
        {
            _updateNeeded = true;
            UpdateLineNumbers();
        }

        #region Private Helpers

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(PredefinedMarginNames.LineNumber);
        }

        private void SetFontFromClassification()
        {
            IClassificationType lineNumberClassificaiton = _classificationTypeRegistry.GetClassificationType("line number");

            var font = _classificationFormatMap.GetTextProperties(lineNumberClassificaiton);

            IClassificationType currentLineNumberClassification = _classificationTypeRegistry.GetClassificationType("Selected Line Number");

            var currentLineNumberFont = _classificationFormatMap.GetTextProperties(currentLineNumberClassification);

            _formatting = font;
            _currentLineFormatting = currentLineNumberFont;

            this.DetermineMarginWidth();
            Layer.BackgroundColor = (font.BackgroundColor ?? NSColor.Clear).CGColor;
            // Reformat all the lines
            ClearLineNumbers();
            this.UpdateLineNumbers();
        }
        
        private void ClearLineNumbers(bool disposing = false)
        {
            recyclableVisuals.Clear();

            if (!disposing)
            {
                foreach (var sublayer in _translatedCanvas.Layer.Sublayers ?? Array.Empty<CALayer>())
                    sublayer.RemoveFromSuperLayer();
            }
        }

        /// <summary>
        /// Determine the width of the margin, using the number of visible digits (e.g. 5) to construct
        /// a model string (e.g. "88888")
        /// </summary>
        private void DetermineMarginWidth()
        {
            // Our width should follow the following rules:
            //  1) No smaller than wide enough to fit 5 digits
            //  2) Increase in size whenever larger numbers are encountered
            //  3) _Never_ decrease in size (e.g. if resized to fit 7 digits, 
            //     will not shrink when scrolled up to 3 digit numbers)

            using (var textLine = new CTLine(new NSAttributedString(new string('8', _visibleDigits), _formatting)))
            {
                intrinsicContentSize = new CGSize(textLine.GetBounds(0).Width, NoIntrinsicMetric);
                InvalidateIntrinsicContentSize();
            }
        }

        private CGSize intrinsicContentSize;
        private NSTrackingArea _trackingArea;

        public override CGSize IntrinsicContentSize => intrinsicContentSize;

        /// <summary>
        /// Resize the width of the margin if the number of digits in the last (largest) visible number
        /// is larger than we can currently handle.
        /// </summary>
        /// <param name="lastVisibleLineNumber">The last (largest) visible number in the view</param>
        private void ResizeIfNecessary(int lastVisibleLineNumber)
        {
            // We are looking at lines base 1, not base 0
            lastVisibleLineNumber++;

            if (lastVisibleLineNumber <= 0)
                lastVisibleLineNumber = 1;

            int numDigits = (int)Math.Log10(lastVisibleLineNumber) + 1;

            if (numDigits > _visibleDigits)
            {
                _visibleDigits = numDigits;
                this.DetermineMarginWidth();

                // Clear existing children so they are all regenerated in
                // UpdateLineNumbers
                ClearLineNumbers();
            }
        }

        private void UpdateLineNumbers()
        {
            _updateNeeded = false;
            int currentLineNumber = _textView.Caret.Position.BufferPosition.GetContainingLine().LineNumber + 1;

            if (!Enabled)
            {
                if (recyclableVisuals.Count > 0)
                {
                    intrinsicContentSize = new CGSize(0, NoIntrinsicMetric);
                    InvalidateIntrinsicContentSize();
                    ClearLineNumbers();
                }
                return;
            }
            else
            {
                if (intrinsicContentSize.Width == 0)
                {
                    DetermineMarginWidth();
                }
            }

            if (_textView.IsClosed)
            {
                return;
            }

            // If the text view is in the middle of performing a layout, don't proceed.  Otherwise an exception will be thrown when trying to access TextViewLines.
            // If we are in layout, then a LayoutChangedEvent is expected to be raised which will in turn cause this method to be invoked.
            if (_textView.InLayout)
            {
                return;
            }

            var (lines, maxLineNumber) = _lineNumbersCalculator.CalculateLineNumbers();

            // If there are no line numbers to display, quit
            if (lines == null || lines.Count == 0)
            {
                return;
            }

            // Check to see if we need to resize the margin width
            this.ResizeIfNecessary(maxLineNumber);

            var layer = _translatedCanvas.Layer;
            unusedChildIndicies.Clear();

            // Update the existing visuals.
            for (int iChild = 0, nChildren = recyclableVisuals.Count; iChild < nChildren; iChild++)
            {
                var child = recyclableVisuals[iChild];
                var lineNumber = child.LineNumber;

                if (lines.TryGetValue(lineNumber, out var line))
                {
                    lines.Remove(lineNumber);
                    UpdateVisual(child, line);
                }
                else
                {
                    child.InUse = false;
                    unusedChildIndicies.Enqueue(iChild);
                }
            }

            // For any leftover lines, repurpose any existing visuals or create new ones.
            foreach (var line in lines.Values)
            {
                CocoaLineNumberMarginDrawingVisual visual;

                if (unusedChildIndicies.Count > 0)
                {
                    var childIndex = unusedChildIndicies.Dequeue();
                    visual = recyclableVisuals[childIndex];
                    Debug.Assert(!visual.InUse);
                }
                else
                {
                    visual = new CocoaLineNumberMarginDrawingVisual();
                    recyclableVisuals.Add(visual);
                    layer.AddSublayer(visual);
                    visual.ContentsScale = layer.ContentsScale;
                }

                UpdateVisual(visual, line);
            }

            if (_oldViewportTop != _textView.ViewportTop)
            {
                _translatedCanvas.Bounds = new CGRect(_translatedCanvas.Bounds.X, _textView.ViewportTop, _translatedCanvas.Bounds.Width, _translatedCanvas.Bounds.Height);
                _oldViewportTop = _textView.ViewportTop;
            }

            void UpdateVisual(CocoaLineNumberMarginDrawingVisual visual, Line line)
            {
                visual.InUse = true;

                var formatting = _formatting;

                if (line.LineNumber == currentLineNumber)
                {
                    formatting = _currentLineFormatting;
                }

                visual.Update(
                    formatting,
                    line,
                    intrinsicContentSize.Width);
            }
        }

        #endregion

        #region Event Handlers

        internal void OnEditorLayoutChanged(object sender, EventArgs e)
        {
            if (!_updateNeeded)
            {
                _updateNeeded = true;
                UpdateLineNumbers();
            }
        }

        private void OnClassificationFormatChanged(object sender, EventArgs e)
        {
            this.SetFontFromClassification();
        }

        #endregion // Event Handlers

        #region ICocoaTextViewMargin Members

        public NSView VisualElement
        {
            get
            {
                ThrowIfDisposed();
                return this;
            }
        }

        #endregion

        #region ITextViewMargin Members

        public double MarginSize
        {
            get
            {
                ThrowIfDisposed();
                return 10;
            }
        }

        public bool Enabled
        {
            get
            {
                ThrowIfDisposed();
                return _localSettings.Number || _localSettings.RelativeNumber;

            }
        }
        #endregion

        public override void UpdateTrackingAreas()
        {
            if (_trackingArea != null)
            {
                RemoveTrackingArea(_trackingArea);
                _trackingArea.Dispose();
            }
            _trackingArea = new NSTrackingArea(Bounds,
                NSTrackingAreaOptions.MouseMoved |
                NSTrackingAreaOptions.ActiveInKeyWindow |
                NSTrackingAreaOptions.MouseEnteredAndExited, this, null);
            this.AddTrackingArea(_trackingArea);
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return string.Compare(marginName, RelativeLineNumbersMarginFactory.LineNumbersMarginName, StringComparison.OrdinalIgnoreCase) == 0 ? this : (ITextViewMargin)null;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                RemoveFromSuperview();
                ClearLineNumbers(true);
            }

            _isDisposed = true;

            base.Dispose(disposing);
        }
    }
}
