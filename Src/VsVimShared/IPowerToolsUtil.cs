
namespace Vim.VisualStudio
{
    /// <summary>
    /// Interface for querying information about the Pro PowerTools extension
    /// </summary>
    internal interface IPowerToolsUtil
    {
        /// <summary>
        /// Is the quick find feature currently active and running
        /// </summary>
        bool IsQuickFindActive { get; }
    }
}
