using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Vim.UnitTest.Mock
{
    public class MockMultiSelection
    {
        private readonly IVimHost _vimHost;

        private bool _shouldIgnoreEvents;

        public bool IsMultiSelectionSupported { get; set; }
        public Dictionary<ITextView, List<SelectedSpan>> SecondarySelectedSpans { get; set; }

        public MockMultiSelection(IVimHost vimHost)
        {
            _vimHost = vimHost;
            SecondarySelectedSpans = new Dictionary<ITextView, List<SelectedSpan>>();
        }

        public void Clear()
        {
            SecondarySelectedSpans.Clear();
        }

        public bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            return TryCustomProcessMultiSelection(textView, command);
        }

        public void SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            var allSelectedSpans = selectedSpans.ToArray();
            var primarySelectedSpan = allSelectedSpans[0];
            if (IsMultiSelectionSupported)
            {
                if (textView.Selection.Mode != TextSelectionMode.Stream)
                {
                    SecondarySelectedSpans.Clear();
                }
                else
                {
                    SecondarySelectedSpans[textView] =
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
                    SetPrimarySelectedSpan(textView, primarySelectedSpan);
                }
                finally
                {
                    _shouldIgnoreEvents = false;
                }
            }
        }

        public IEnumerable<SelectedSpan> GetSelectedSpans(ITextView textView)
        {
            var primarySelectedSpans = new[] { GetPrimarySelectedSpan(textView) };
            if (IsMultiSelectionSupported)
            {
                if (textView.Selection.Mode != TextSelectionMode.Stream)
                {
                    return primarySelectedSpans;
                }
                return primarySelectedSpans.Concat(GetSecondarySelectedSpans(textView));
            }
            else
            {
                return primarySelectedSpans;
            }
        }

        private SelectedSpan GetPrimarySelectedSpan(ITextView textView)
        {
            return new SelectedSpan(
                textView.Caret.Position.VirtualBufferPosition,
                textView.Selection.AnchorPoint,
                textView.Selection.ActivePoint);
        }

        private List<SelectedSpan> GetSecondarySelectedSpans(ITextView textView)
        {
            if (SecondarySelectedSpans.TryGetValue(textView, out var list))
            {
                return list;
            }
            return new List<SelectedSpan>();
        }

        private void SetPrimarySelectedSpan(ITextView textView, SelectedSpan primarySelectedSpan)
        {
            textView.Caret.MoveTo(primarySelectedSpan.CaretPoint);
            if (primarySelectedSpan.Length != 0)
            {
                textView.Selection.Select(primarySelectedSpan.AnchorPoint, primarySelectedSpan.ActivePoint);
            }
        }

        public void RegisterVimBuffer(IVimBuffer vimBuffer)
        {
            if (IsMultiSelectionSupported)
            {
                var textView = vimBuffer.TextView;
                void clearSecondarySelections(object sender, EventArgs e)
                {
                    ClearSecondarySelections(textView);
                }
                textView.Selection.SelectionChanged += clearSecondarySelections;
                textView.Caret.PositionChanged += clearSecondarySelections;
                void unsubscribe(object sender, EventArgs e)
                {
                    ClearSecondarySelections(textView);
                    textView.Selection.SelectionChanged -= clearSecondarySelections;
                    textView.Caret.PositionChanged -= clearSecondarySelections;
                }
                vimBuffer.Closed += unsubscribe;
            }
        }

        private void ClearSecondarySelections(ITextView textView)
        {
            if (!_shouldIgnoreEvents && GetSecondarySelectedSpans(textView).Count > 0)
            {
                SecondarySelectedSpans.Clear();
            }
        }

        private bool TryCustomProcessMultiSelection(ITextView textView, InsertCommand command)
        {
            if (command.TryGetInsertionText(out var text))
            {
                // Simulate editor support for simultaneouos insertion
                // at all carets at the same time.
                InsertAtAllCarets(_vimHost, textView, text);
                return true;
            }
            return false;
        }

        private static void InsertAtAllCarets(IVimHost vimHost, ITextView textView, string text)
        {
            var oldSpans = vimHost.GetSelectedSpans(textView).ToArray();
            using (var textEdit = textView.TextBuffer.CreateEdit())
            {
                foreach (var span in oldSpans)
                {
                    textEdit.Insert(span.CaretPoint.Position.Position, text);
                }
                textEdit.Apply();
            }
            var snapshot = textView.TextBuffer.CurrentSnapshot;
            var newSpans =
                oldSpans
                .Select(span => span.CaretPoint)
                .Select(point => point.MapToSnapshot(snapshot))
                .Select(point => point.Add(text.Length))
                .Select(point => new SelectedSpan(point))
                .ToArray();
            vimHost.SetSelectedSpans(textView, newSpans);
        }
    }
}
