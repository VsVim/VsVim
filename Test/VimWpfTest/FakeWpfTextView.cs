using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf.UnitTest
{
    class FakeWpfTextView : IWpfTextView
    {
        public PropertyCollection Properties { get; private set; }

        public void DisplayTextLineContainingBufferPosition(SnapshotPoint bufferPosition, double verticalDistance,
                                                            ViewRelativePosition relativeTo)
        {
            throw new NotImplementedException();
        }

        public void DisplayTextLineContainingBufferPosition(SnapshotPoint bufferPosition, double verticalDistance,
                                                            ViewRelativePosition relativeTo, double? viewportWidthOverride,
                                                            double? viewportHeightOverride)
        {
            throw new NotImplementedException();
        }

        public SnapshotSpan GetTextElementSpan(SnapshotPoint point)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void QueueSpaceReservationStackRefresh()
        {
            throw new NotImplementedException();
        }

        public IWpfTextViewLine GetTextViewLineContainingBufferPosition(SnapshotPoint bufferPosition)
        {
            throw new NotImplementedException();
        }

        public bool InLayout { get; private set; }
        public IViewScroller ViewScroller { get; private set; }
        public IWpfTextViewLineCollection TextViewLines { get; private set; }
        public IFormattedLineSource FormattedLineSource { get; private set; }
        public ILineTransformSource LineTransformSource { get; private set; }
        public double ZoomLevel { get; set; }
        public event EventHandler<BackgroundBrushChangedEventArgs> BackgroundBrushChanged;
        public event EventHandler<ZoomLevelChangedEventArgs> ZoomLevelChanged;
        public IAdornmentLayer GetAdornmentLayer(string name)
        {
            throw new NotImplementedException();
        }

        public ISpaceReservationManager GetSpaceReservationManager(string name)
        {
            throw new NotImplementedException();
        }

        public FrameworkElement VisualElement { get { return new FrameworkElement(); } }
        public Brush Background { get; set; }

        ITextViewLineCollection ITextView.TextViewLines
        {
            get { return TextViewLines; }
        }

        public ITextCaret Caret { get; private set; }
        public ITextSelection Selection { get; private set; }
        public ITrackingSpan ProvisionalTextHighlight { get; set; }
        public ITextViewRoleSet Roles { get; private set; }
        public ITextBuffer TextBuffer { get; private set; }
        public IBufferGraph BufferGraph { get; private set; }
        public ITextSnapshot TextSnapshot { get; private set; }
        public ITextSnapshot VisualSnapshot { get; private set; }
        public ITextViewModel TextViewModel { get; private set; }
        public ITextDataModel TextDataModel { get; private set; }
        public double MaxTextRightCoordinate { get; private set; }
        public double ViewportLeft { get; set; }
        public double ViewportTop { get; private set; }
        public double ViewportRight { get; private set; }
        public double ViewportBottom { get; private set; }
        public double ViewportWidth { get; private set; }
        public double ViewportHeight { get; private set; }
        public double LineHeight { get; private set; }
        public bool IsClosed { get; private set; }
        public IEditorOptions Options { get; private set; }
        public bool IsMouseOverViewOrAdornments { get; private set; }
        public bool HasAggregateFocus { get; private set; }
        public event EventHandler<TextViewLayoutChangedEventArgs> LayoutChanged;
        public event EventHandler ViewportLeftChanged;
        public event EventHandler ViewportHeightChanged;
        public event EventHandler ViewportWidthChanged;
        public event EventHandler<MouseHoverEventArgs> MouseHover;
        public event EventHandler Closed;
        public event EventHandler LostAggregateFocus;
        public event EventHandler GotAggregateFocus;
        ITextViewLine ITextView.GetTextViewLineContainingBufferPosition(SnapshotPoint bufferPosition)
        {
            return GetTextViewLineContainingBufferPosition(bufferPosition);
        }
    }
}
