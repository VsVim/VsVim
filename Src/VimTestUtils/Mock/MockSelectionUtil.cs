using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vim.UnitTest.Mock
{
    public class MockSelectionUtil : ISelectionUtil
    {
        private readonly ITextView _textView;

        private bool _shouldIgnoreEvents;

        public bool IsMultiSelectionSupported { get; set; }
        public List<SelectedSpan> SecondarySelectedSpans { get; set; }

        public MockSelectionUtil(ITextView textView, bool isMultiSelectionSupported)
        {
            _textView = textView;
            IsMultiSelectionSupported = isMultiSelectionSupported;
            SecondarySelectedSpans = new List<SelectedSpan>();
            InstallHandlers();
        }

        public void Clear()
        {
            SecondarySelectedSpans.Clear();
        }

        public bool TryCustomProcess(InsertCommand command)
        {
            if (IsMultiSelectionSupported)
            {
                return TryCustomProcessMultiSelection(command);
            }
            return false;
        }

        public void SetSelectedSpans(IEnumerable<SelectedSpan> selectedSpans)
        {
            var allSelectedSpans = selectedSpans.ToArray();
            var primarySelectedSpan = allSelectedSpans[0];
            if (IsMultiSelectionSupported)
            {
                if (_textView.Selection.Mode != TextSelectionMode.Stream)
                {
                    SecondarySelectedSpans.Clear();
                }
                else
                {
                    SecondarySelectedSpans =
                        allSelectedSpans
                        .Skip(1)
                        .OrderBy(span => span.CaretPoint.Position.Position)
                        .ToList();
                }
            }
            if (!_shouldIgnoreEvents)
            {
                try
                {
                    _shouldIgnoreEvents = true;
                    SetPrimarySelectedSpan(primarySelectedSpan);
                }
                finally
                {
                    _shouldIgnoreEvents = false;
                }
            }
        }

        public IEnumerable<SelectedSpan> GetSelectedSpans()
        {
            var primarySelectedSpans = new[] { GetPrimarySelectedSpan() };
            if (IsMultiSelectionSupported)
            {
                if (_textView.Selection.Mode != TextSelectionMode.Stream)
                {
                    return primarySelectedSpans;
                }
                return primarySelectedSpans.Concat(SecondarySelectedSpans);
            }
            else
            {
                return primarySelectedSpans;
            }
        }

        private SelectedSpan GetPrimarySelectedSpan()
        {
            return new SelectedSpan(
                _textView.Caret.Position.VirtualBufferPosition,
                _textView.Selection.AnchorPoint,
                _textView.Selection.ActivePoint);
        }

        private void SetPrimarySelectedSpan(SelectedSpan primarySelectedSpan)
        {
            _textView.Caret.MoveTo(primarySelectedSpan.CaretPoint);
            if (primarySelectedSpan.Length != 0)
            {
                _textView.Selection.Select(primarySelectedSpan.AnchorPoint, primarySelectedSpan.ActivePoint);
            }
        }

        private void InstallHandlers()
        {
            if (IsMultiSelectionSupported)
            {
                void clearSecondarySelections(object sender, EventArgs e)
                {
                    ClearSecondarySelections();
                }
                _textView.Selection.SelectionChanged += clearSecondarySelections;
                _textView.Caret.PositionChanged += clearSecondarySelections;
                void unsubscribe(object sender, EventArgs e)
                {
                    ClearSecondarySelections();
                    _textView.Selection.SelectionChanged -= clearSecondarySelections;
                    _textView.Caret.PositionChanged -= clearSecondarySelections;
                }
                _textView.Closed += unsubscribe;
            }
        }

        private void ClearSecondarySelections()
        {
            if (!_shouldIgnoreEvents && SecondarySelectedSpans.Count > 0)
            {
                SecondarySelectedSpans.Clear();
            }
        }

        private bool TryCustomProcessMultiSelection(InsertCommand command)
        {
            if (command.TryGetInsertionText(out var text))
            {
                // Simulate editor support for simultaneouos insertion
                // at all carets at the same time.
                InsertAtAllCarets(text);
                return true;
            }
            return false;
        }

        private void InsertAtAllCarets(string text)
        {
            var oldSpans = GetSelectedSpans().ToArray();
            using (var textEdit = _textView.TextBuffer.CreateEdit())
            {
                foreach (var span in oldSpans)
                {
                    textEdit.Insert(span.CaretPoint.Position.Position, text);
                }
                textEdit.Apply();
            }
            var snapshot = _textView.TextBuffer.CurrentSnapshot;
            var newSpans =
                oldSpans
                .Select(span => span.CaretPoint)
                .Select(point => point.TranslateTo(snapshot, PointTrackingMode.Negative))
                .Select(point => point.Add(text.Length))
                .Select(point => new SelectedSpan(point))
                .ToArray();
            SetSelectedSpans(newSpans);
        }
    }
}
