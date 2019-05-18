#if VS_SPECIFIC_2019
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text;
using Vim;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Async
{
    [Name(VimSpecificUtil.MefNamePrefix + "Async Completion Source")]
    [ContentType(VimConstants.AnyContentType)]
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [Order(Before = "Roslyn Completion Source Provider")]
    internal sealed class WordAsyncCompletionSourceProvider : IAsyncCompletionSourceProvider
    {
        private readonly SVsServiceProvider _vsServiceProvider;

        [ImportingConstructor]
        internal WordAsyncCompletionSourceProvider(SVsServiceProvider vsServiceProvider)
        {
            _vsServiceProvider = vsServiceProvider;
        }

        IAsyncCompletionSource IAsyncCompletionSourceProvider.GetOrCreate(ITextView textView) =>
            VimSpecificUtil.IsTargetVisualStudio(_vsServiceProvider)
                ? new WordAsyncCompletionSource(textView)
                : null;
    }   
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
