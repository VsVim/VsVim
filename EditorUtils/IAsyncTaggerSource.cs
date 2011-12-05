
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
namespace EditorUtils
{
    /// <summary>
    /// A tagger source for asynchronous taggers.  This interface is consumed from multiple threads
    /// and each method which is called on the background thread is labelled as such
    /// be called on any thread
    /// </summary>
    public interface IAsyncTaggerSource<TData, TTag>
        where TTag : ITag
    {
        /// <summary>
        /// Delay in milliseconds which should occur between the call to GetTags and the kicking off
        /// of a background task
        /// </summary>
        int? Delay { get; }

        /// <summary>
        /// The current Snapshot.  
        ///
        /// Called from the main thread only
        /// </summary>
        ITextSnapshot TextSnapshot { get; }

        /// <summary>
        /// The current ITextView if this tagger is attached to a ITextView.  This is an optional
        /// value
        ///
        /// Called from the main thread only
        /// </summary>
        ITextView TextViewOptional { get; }

        /// <summary>
        /// This method is called to gather data on the UI thread which will then be passed
        /// down to the background thread for processing
        ///
        /// Called from the main thread only
        /// </summary>
        TData GetDataForSpan(SnapshotSpan span);

        /// <summary>
        /// Return the applicable tags for the given SnapshotSpan instance.  This will be
        /// called on a background thread and should respect the provided CancellationToken
        ///
        /// Called from the background thread only
        /// </summary>
        [UsedInBackgroundThread]
        ReadOnlyCollection<ITagSpan<TTag>> GetTagsInBackground(TData data, SnapshotSpan span, CancellationToken cancellationToken);

        /// <summary>
        /// To prevent needless spawning of Task<T> values the async tagger has the option
        /// of providing prompt data.  This method should only be used when determination
        /// of the tokens requires no calculation.
        ///
        /// Called from the main thread only
        /// <summary>
        bool TryGetTagsPrompt(SnapshotSpan span, out IEnumerable<ITagSpan<TTag>> tags);

        /// <summary>
        /// Raised by the source when the underlying source has changed.  All previously
        /// provided data should be considered incorrect after this event
        /// </summary>
        event EventHandler Changed;
    }
}
