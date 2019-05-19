using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Text;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion
{
    internal static class Extensions
    {
        /// <summary>
        /// This is inserted into every <see cref="ITextView"/> property bag which is created for
        /// a word completion.  It's used to pass the <see cref="VimWordCompletionData"/> instance to the 
        /// legacy and async completion APIs
        /// </summary>
        internal static object WordCompletionDataSessionKey = new object();

        internal static void SetWordCompletionData(this ITextView textView, VimWordCompletionData wordCompletionData) =>
            textView.Properties[WordCompletionDataSessionKey] = wordCompletionData;

        internal static void ClearWordCompletionData(this ITextView textView) =>
            textView.Properties.RemoveProperty(WordCompletionDataSessionKey);

        internal static bool TryGetWordCompletionData(this ITextView textView, out VimWordCompletionData wordCompletionData) =>
            textView.Properties.TryGetProperty(WordCompletionDataSessionKey, out wordCompletionData);

        internal static VimWordCompletionData TryGetWordCompletionData(this ITextView textView) =>
            textView.Properties.TryGetProperty(WordCompletionDataSessionKey, out VimWordCompletionData wordCompletionData)
            ? wordCompletionData
            : null;
    }
}
