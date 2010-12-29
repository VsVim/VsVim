
namespace VsVim
{
    internal interface IExternalEditorManager
    {
        /// <summary>
        /// Is the ReSharper package installed
        /// </summary>
        bool IsResharperInstalled { get; }

        /// <summary>
        /// Is the ReSharper package currently loaded.  While the package may not be loaded 
        /// on startup it can be loaded at a later point.  Once it's loaded though it won't
        /// ever be unloaded.
        /// </summary>
        bool IsResharperLoaded { get; }
    }
}
