using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.ReSharper
{
    internal sealed class ReSharperKeyProcessor : KeyProcessor
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;

        internal ReSharperKeyProcessor(IVimBufferCoordinator vimBufferCoordinator)
        {
            _vimBufferCoordinator = vimBufferCoordinator;
            _vimBuffer = vimBufferCoordinator.VimBuffer;
        }
    }
}
