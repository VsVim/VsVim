using Microsoft.FSharp.Core;
using Vim;
namespace Vim.VisualStudio
{
    /// <summary>
    /// Coordinates events / items specific to an IVimBuffer which are needed across multiple
    /// Visual Studio components
    /// </summary>
    internal interface IVimBufferCoordinator
    {
        /// <summary>
        /// The IVimBuffer this is coordinating
        /// </summary>
        IVimBuffer VimBuffer { get; }

        /// <summary>
        /// True if there is a KeyInput value currently being discarded
        /// </summary>
        bool HasDiscardedKeyInput { get; }

        /// <summary>
        /// Is this KeyInput value already discarded by the input system?
        /// </summary>
        bool IsDiscarded(KeyInput keyInput);

        /// <summary>
        /// Discard this KeyInput for the duration of the current key input scenario
        /// </summary>
        void Discard(KeyInput keyInput);
    }

    internal interface IVimBufferCoordinatorFactory
    {
        /// <summary>
        /// Get the IVimBufferCoordinator for the given IVimBuffer
        /// </summary>
        IVimBufferCoordinator GetVimBufferCoordinator(IVimBuffer buffer);
    }
}
