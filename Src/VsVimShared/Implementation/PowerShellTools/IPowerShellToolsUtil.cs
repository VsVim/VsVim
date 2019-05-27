
namespace Vim.VisualStudio.Implementation.PowerShellTools
{
    /// <summary>
    /// Interface for getting information about the PowerShell Tools for Visual Studio install. 
    /// </summary>
    internal interface IPowerShellToolsUtil
    {
        bool IsInstalled { get; }
    }
}
