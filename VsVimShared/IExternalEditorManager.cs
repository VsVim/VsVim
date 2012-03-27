
namespace VsVim
{
    internal interface IExternalEditorManager
    {
        /// <summary>
        /// Is the ReSharper package installed
        /// </summary>
        bool IsResharperInstalled { get; }
    }
}
