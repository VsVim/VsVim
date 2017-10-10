using Microsoft.VisualStudio.Text.Operations;

namespace EditorUtils
{
    /// <summary>
    /// In certain hosted scenarios the default ITextUndoHistoryRegistry won't be 
    /// available.  This is a necessary part of editor composition though and some 
    /// implementation needs to be provided.  Importing this type will provide a 
    /// very basic implementation
    ///
    /// This type intentionally doesn't ever export ITextUndoHistoryRegistry.  Doing
    /// this would conflict with Visual Studios export and cause a MEF composition 
    /// error.  It's instead exposed via this interface 
    ///
    /// In general this type won't be used except in testing
    /// </summary>
    public interface IBasicUndoHistoryRegistry
    {
        /// <summary>
        /// Get the basic implementation of the ITextUndoHistoryRegistry
        /// </summary>
        ITextUndoHistoryRegistry TextUndoHistoryRegistry { get; }

        /// <summary>
        /// Try and get the IBasicUndoHistory for the given context
        /// </summary>
        bool TryGetBasicUndoHistory(object context, out IBasicUndoHistory basicUndoHistory);
    }

    public interface IBasicUndoHistory : ITextUndoHistory
    {
        /// <summary>
        /// Clear out all of the state including the undo and redo stacks
        /// </summary>
        void Clear();
    }
}
