using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Vim.VisualStudio.Specific.Implementation.WordCompletion.Legacy
{
    /// <summary>
    /// This type is responsible for providing word completion sessions over a given ITextView
    /// instance and given set of words.
    /// 
    /// Properly integrating with the IntelliSense stack here is a bit tricky.  In order to participate
    /// in any completion session you must provide an ICompletionSource for the lifetime of the 
    /// ITextView.  Ideally we don't want to provide any completion information unless we are actually
    /// starting a word completion session
    /// </summary>
    [Name(VimSpecificUtil.MefNamePrefix + "Legacy Completion Source")]
    [ContentType(VimConstants.AnyContentType)]
    [Export(typeof(ICompletionSourceProvider))]
    internal sealed class WordLegacyCompletionSourceProvider: VimSpecificService, ICompletionSourceProvider
    {
        [ImportingConstructor]
        internal WordLegacyCompletionSourceProvider(Lazy<IVimHost> vimHost)
            :base(vimHost)
        {

        }

        #region ICompletionSourceProvider

        ICompletionSource ICompletionSourceProvider.TryCreateCompletionSource(ITextBuffer textBuffer)
        {
            return InsideValidHost
                ? new WordLegacyCompletionSource(textBuffer)
                : null;
        }

        #endregion
    }
}
