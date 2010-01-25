using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.FSharp.Core;
using Vim;
using System.Windows.Input;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest
{
    public static class Util
    {
        #region Option

        public static bool IsNone<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsNone(option);
        }

        public static bool IsSome<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsSome(option);
        }

        public static bool HasValue<T>(this FSharpOption<T> option)
        {
            return FSharpOption<T>.get_IsSome(option);
        }

        #endregion

        #region IMode

        public static bool CanProcess(this IMode mode, Key key)
        {
            return mode.CanProcess(InputUtil.KeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, Key key)
        {
            return mode.Process(InputUtil.KeyToKeyInput(key));
        }

        public static ProcessResult Process(this IMode mode, char c)
        {
            return mode.Process((InputUtil.CharToKeyInput(c)));
        }

        public static ProcessResult Process(this IMode mode, string input)
        {
            ProcessResult last = null;
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                last = mode.Process(c);
            }

            return last;
        }

        #endregion

        #region IVimBuffer

        public static void ProcessInputAsString(this IVimBuffer buf, string input)
        {
            foreach (var c in input)
            {
                var i = InputUtil.CharToKeyInput(c);
                buf.ProcessInput(i);
            }
        }

        #endregion

        #region ITextView

        public static SnapshotSpan GetLineSpan(this ITextView textView, int startLine, int endLine)
        {
            return textView.TextSnapshot.GetLineSpan(startLine, endLine);
        }

        #endregion 

        #region ITextSnapshot

        public static SnapshotSpan GetLineSpan(this ITextSnapshot tss, int startLine, int endLine)
        {
            var start = tss.GetLineFromLineNumber(startLine);
            var end = tss.GetLineFromLineNumber(endLine);
            return new SnapshotSpan(start.Start, end.EndIncludingLineBreak);
        }

        #endregion

    }
}
