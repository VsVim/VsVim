#if VS_SPECIFIC_2019
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using Vim;

namespace Vim.Implementation.WordCompletion.Async
{
    [Name("Vim Async Word Completion Session Factory Service")]
    [ContentType(VimConstants.AnyContentType)]
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [Order(Before = "Roslyn Completion Source Provider")]
    internal sealed class WordAsyncCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        IAsyncCompletionSource IAsyncCompletionSourceProvider.GetOrCreate(ITextView textView) => new WordAsyncCompletionSource(textView);
    }   
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
