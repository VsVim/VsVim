using Vim;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.FSharp.Core;

namespace VsVim
{
    /// <summary>
    /// Factory for producing IVersionService intstances.  This is an interface for services which
    /// need to vary in implementation between versions of Visual Studio
    /// </summary>
    public interface ISharedService
    {
        /// <summary>
        /// Returns the ITextView which should have keyboard focus.  This method is used during macro
        /// running and hence must account for view changes which occur during a macro run.  Say by the
        /// macro containing the 'gt' command.  Unfortunately these don't fully process through Visual
        /// Studio until the next UI thread pump so we instead have to go straight to the view controller
        /// </summary>
        bool TryGetFocusedTextView(out ITextView textView);

        /// <summary>
        /// Go to the next tab in the specified direction
        /// </summary>
        void GoToNextTab(Path path, int count);

        /// <summary>
        /// Go to the tab with the specified index
        /// </summary>
        void GoToTab(int index);
    }

    /// <summary>
    /// Factory which is associated with a specific version of Visual Studio
    /// </summary>
    public interface ISharedServiceVersionFactory
    {
        /// <summary>
        /// Version of Visual Studio this implementation is tied to
        /// </summary>
        VisualStudioVersion Version { get; }

        ISharedService Create();
    }

    /// <summary>
    /// Consumable interface which will provide an ISharedService implementation.  This is a MEF 
    /// importable component
    /// </summary>
    public interface ISharedServiceFactory
    {
        /// <summary>
        /// Create an instance of IVsSharedService if it's applicable for the current version
        /// </summary>
        ISharedService Create();
    }
}
