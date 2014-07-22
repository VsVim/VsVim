
namespace Vim.VisualStudio.Implementation.ReSharper
{
    /// <summary>
    /// Interface for getting information about the R# install.  
    /// </summary>
    internal interface IReSharperUtil
    {
        bool IsInstalled { get; }
    }
}
