using Microsoft.FSharp.Core;
using Vim;
namespace VsVim
{
    /// <summary>
    /// Coordinates events / items specific to an IVimBuffer which are needed across multiple
    /// Visual Studio components coordinator
    /// </summary>
    internal interface IVimBufferCoordinator
    {
        /// <summary>
        /// The IVimBuffer this is coordinating
        /// </summary>
        IVimBuffer VimBuffer { get; }

        /// <summary>
        /// When this is set to a KeyInput we should discard and set as handled an KeyInput
        /// which originates from the user that matchs the value
        /// </summary>
        FSharpOption<KeyInput> DiscardedKeyInput { get; set; }
    }

    internal interface IVimBufferCoordinatorFactory
    {
        /// <summary>
        /// Get the IVimBufferCoordinator for the given IVimBuffer
        /// </summary>
        IVimBufferCoordinator GetVimBufferCoordinator(IVimBuffer buffer);
    }
}
