#if VS_SPECIFIC_2017
#else

using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

namespace Vim.UI.Wpf.Implementation.MultiSelection
{
    [Export(typeof(ISelectionUtilFactory))]
    internal sealed class MultiSelectionUtilFactory : ISelectionUtilFactory
    {
        private static readonly object s_key = new object();

        private sealed class MultiSelectionUtil : ISelectionUtil
        {
            private readonly ITextView _textView;

            public MultiSelectionUtil(ITextView textView)
            {
                _textView = textView;
            }

            bool ISelectionUtil.IsMultiSelectionSupported => true;

            IEnumerable<SelectionSpan> ISelectionUtil.GetSelectedSpans()
            {
                if (_textView.Selection.Mode == TextSelectionMode.Box)
                {
                    var caretPoint = _textView.Caret.Position.VirtualBufferPosition;
                    var anchorPoint = _textView.Selection.AnchorPoint;
                    var activePoint = _textView.Selection.ActivePoint;
                    return new[] { new SelectionSpan(caretPoint, anchorPoint, activePoint) };
                }

                var broker = _textView.GetMultiSelectionBroker();
                var primarySelection = broker.PrimarySelection;
                var secondarySelections = broker.AllSelections
                    .Where(span => span != primarySelection)
                    .Select(selection => GetSelectedSpan(selection));
                return new[] { GetSelectedSpan(primarySelection) }.Concat(secondarySelections);
            }

            void ISelectionUtil.SetSelectedSpans(IEnumerable<SelectionSpan> selectedSpans)
            {
                SetSelectedSpansCore(selectedSpans.ToArray());
            }

            private void SetSelectedSpansCore(SelectionSpan[] selectedSpans)
            {
                if (_textView.Selection.Mode == TextSelectionMode.Box)
                {
                    var selectedSpan = selectedSpans[0];
                    _textView.Caret.MoveTo(selectedSpan.CaretPoint);
                    if (selectedSpan.Length == 0)
                    {
                        _textView.Selection.Clear();
                    }
                    else
                    {
                        _textView.Selection.Select(selectedSpan.AnchorPoint, selectedSpan.ActivePoint);
                    }
                    return;
                }

                var selections = new Selection[selectedSpans.Length];
                for (var caretIndex = 0; caretIndex < selectedSpans.Length; caretIndex++)
                {
                    selections[caretIndex] = GetSelection(selectedSpans[caretIndex]);
                }
                var broker = _textView.GetMultiSelectionBroker();
                broker.SetSelectionRange(selections, selections[0]);
            }

            private static SelectionSpan GetSelectedSpan(Selection selection)
            {
                return new SelectionSpan(selection.InsertionPoint, selection.AnchorPoint, selection.ActivePoint);
            }

            private static Selection GetSelection(SelectionSpan span)
            {
                return new Selection(span.CaretPoint, span.AnchorPoint, span.ActivePoint);
            }
        }

        [ImportingConstructor]
        internal MultiSelectionUtilFactory()
        {
        }

        ISelectionUtil ISelectionUtilFactory.GetSelectionUtil(ITextView textView)
        {
            var propertyCollection = textView.Properties;
            return propertyCollection.GetOrCreateSingletonProperty(s_key,
                () => new MultiSelectionUtil(textView));
        }
    }
}

#endif
