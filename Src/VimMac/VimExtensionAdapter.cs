using Microsoft.VisualStudio.Text.Editor;

//TODO: Copied from VsVimShared
namespace Vim.VisualStudio
{
    /// <summary>
    /// This base class simplifies the implementation of extension adapters by
    /// providing a default implementation for all extension points. If new
    /// extension points are added, no existing extension adapters will need to
    /// be changed unless they wish to participate in the new extension point.
    /// </summary>
    internal class VimExtensionAdapter : IExtensionAdapter
    {
        protected virtual bool IsUndoRedoExpected =>
            false;

        protected virtual bool ShouldCreateVimBuffer(ITextView textView) =>
            true;

        protected virtual bool IsIncrementalSearchActive(ITextView textView) =>
            false;

        protected virtual bool UseDefaultCaret =>
            false;

        private bool? Unless(bool expected, bool value)
        {
            if (value != expected)
            {
                return value;
            }
            return null;
        }

        bool? IExtensionAdapter.IsUndoRedoExpected =>
            Unless(false, IsUndoRedoExpected);

        bool? IExtensionAdapter.ShouldCreateVimBuffer(ITextView textView) =>
            Unless(true, ShouldCreateVimBuffer(textView));

        bool? IExtensionAdapter.IsIncrementalSearchActive(ITextView textView) =>
            Unless(false, IsIncrementalSearchActive(textView));

        bool? IExtensionAdapter.UseDefaultCaret =>
            Unless(false, UseDefaultCaret);
    }
}
