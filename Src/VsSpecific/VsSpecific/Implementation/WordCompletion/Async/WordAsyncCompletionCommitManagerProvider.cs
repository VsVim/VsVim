#if VS_SPECIFIC_2019
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;

namespace VsSpecific.Implementation.WordCompletion.Async
{
    [Name("VsVim async completion commit manager provider")]
    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class WordAsyncCompletionCommitManagerProvier : IAsyncCompletionCommitManagerProvider
    {
        internal WordAsyncCompletionCommitManager GetOrCreate(ITextView textView) => new WordAsyncCompletionCommitManager(textView);

        #region IAsyncCompletionCommitManagerProvider

        IAsyncCompletionCommitManager IAsyncCompletionCommitManagerProvider.GetOrCreate(ITextView textView) => GetOrCreate(textView);

        #endregion
    }
}

#elif VS_SPECIFIC_2015 || VS_SPECIFIC_2017
// Nothing to do
#else
#error Unsupported configuration
#endif
