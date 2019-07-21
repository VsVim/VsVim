#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017
#else

using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Vim;
using Vim.VisualStudio.Specific;

namespace Vim.Specific.Implementation.Selection
{
    [Export(typeof(ISelectionUtilFactory))]
    [Export(typeof(IVimSpecificService))]
    class MultiSelectionUtilFactory : VimSpecificService, ISelectionUtilFactory
    {
        private class MultiSelectionUtil : ISelectionUtil
        {
            private readonly ITextView _textView;

            public MultiSelectionUtil(ITextView textView)
            {
                _textView = textView;
            }

            bool ISelectionUtil.IsMultiSelectionSupported => true;

            IEnumerable<SelectedSpan> ISelectionUtil.GetSelectedSpans()
            {
                if (_textView.Selection.Mode == TextSelectionMode.Box)
                {
                    var caretPoint = _textView.Caret.Position.VirtualBufferPosition;
                    var anchorPoint = _textView.Selection.AnchorPoint;
                    var activePoint = _textView.Selection.ActivePoint;
                    return new[] { new SelectedSpan(caretPoint, anchorPoint, activePoint) };
                }

                var broker = _textView.GetMultiSelectionBroker();
                var primarySelection = broker.PrimarySelection;
                var secondarySelections = broker.AllSelections
                    .Where(span => span != primarySelection)
                    .Select(selection => GetSelectedSpan(selection));
                return new[] { GetSelectedSpan(primarySelection) }.Concat(secondarySelections);
            }

            void ISelectionUtil.SetSelectedSpans(IEnumerable<SelectedSpan> selectedSpans)
            {
                SetSelectedSpansCore(selectedSpans.ToArray());
            }

            private void SetSelectedSpansCore(SelectedSpan[] selectedSpans)
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

                var selections = new Microsoft.VisualStudio.Text.Selection[selectedSpans.Length];
                for (var caretIndex = 0; caretIndex < selectedSpans.Length; caretIndex++)
                {
                    selections[caretIndex] = GetSelection(selectedSpans[caretIndex]);
                }
                var broker = _textView.GetMultiSelectionBroker();
                broker.SetSelectionRange(selections, selections[0]);
            }

            private static SelectedSpan GetSelectedSpan(Microsoft.VisualStudio.Text.Selection selection)
            {
                return new SelectedSpan(selection.InsertionPoint, selection.AnchorPoint, selection.ActivePoint);
            }

            private static Microsoft.VisualStudio.Text.Selection GetSelection(SelectedSpan span)
            {
                return new Microsoft.VisualStudio.Text.Selection(span.CaretPoint, span.AnchorPoint, span.ActivePoint);
            }
        }

        [ImportingConstructor]
        internal MultiSelectionUtilFactory(Lazy<IVimHost> vimHost)
            : base(vimHost)
        {
        }

        ISelectionUtil ISelectionUtilFactory.GetSelectionUtil(ITextView textView)
        {
            return new MultiSelectionUtil(textView);
        }
    }
}

#endif
