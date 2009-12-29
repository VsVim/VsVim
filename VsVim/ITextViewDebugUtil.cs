using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    /// <summary>
    /// Listens to certain editor events that I like to break on while debugging.  Not necessary 
    /// for normal processing
    /// </summary>
    internal static class ITextViewDebugUtil
    {
        [Conditional("DEBUG")]
        internal static void Attach(ITextView textView)
        {
            textView.Caret.PositionChanged += new EventHandler<CaretPositionChangedEventArgs>(OnCaretPositionChanged);
            textView.TextBuffer.Changed += new EventHandler<Microsoft.VisualStudio.Text.TextContentChangedEventArgs>(OnTextBufferChanged);
        }

        [Conditional("DEBUG")]
        internal static void Detach(ITextView textView)
        {
            textView.Caret.PositionChanged -= new EventHandler<CaretPositionChangedEventArgs>(OnCaretPositionChanged);
            textView.TextBuffer.Changed -= new EventHandler<Microsoft.VisualStudio.Text.TextContentChangedEventArgs>(OnTextBufferChanged);
        }

        private static void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {

        }

        private static void OnTextBufferChanged(object sender, Microsoft.VisualStudio.Text.TextContentChangedEventArgs e)
        {
        }

    }
}
