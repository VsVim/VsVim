using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Text;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    internal static class Extensions
    {
        /// <summary>
        /// This is inserted into every <see cref="IAsyncCompletionSession"/> property bag which is created for
        /// a word completion.  It's used to pass the <see cref="WordCompletionData"/> instance to the 
        /// <see cref="WordAsyncCompletionSession"/>
        /// </summary>
        internal static object WordCompletionDataSessionKey = new object();

        internal static void SetWordCompletionData(this ITextView textView, WordCompletionData wordCompletionData) =>
            textView.Properties[WordCompletionDataSessionKey] = wordCompletionData;

        internal static void ClearWordCompletionData(this ITextView textView) =>
            textView.Properties.RemoveProperty(WordCompletionDataSessionKey);

        internal static bool TryGetWordCompletionData(this ITextView textView, out WordCompletionData wordCompletionData) =>
            textView.Properties.TryGetProperty(WordCompletionDataSessionKey, out wordCompletionData);

        internal static WordCompletionData TryGetWordCompletionData(this ITextView textView) =>
            textView.Properties.TryGetProperty(WordCompletionDataSessionKey, out WordCompletionData wordCompletionData)
            ? wordCompletionData
            : null;
    }
}
