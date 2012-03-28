using System.ComponentModel.Composition;
using Microsoft.FSharp.Core;
using Vim;
using Vim.Extensions;

namespace VsVim.Implementation
{
    internal sealed class VimBufferCoordinator : IVimBufferCoordinator
    {
        private readonly IVimBuffer _buffer;
        private FSharpOption<KeyInput> _discardedKeyInput;

        internal VimBufferCoordinator(IVimBuffer buffer)
        {
            _buffer = buffer;
        }

        IVimBuffer IVimBufferCoordinator.VimBuffer
        {
            get { return _buffer; }
        }

        FSharpOption<KeyInput> IVimBufferCoordinator.DiscardedKeyInput
        {
            get { return _discardedKeyInput; } 
            set { _discardedKeyInput = value; } 
        }
    }

    [Export(typeof(IVimBufferCoordinatorFactory))]
    internal sealed class VimBufferCoordinatorFactory : IVimBufferCoordinatorFactory
    {
        /// <summary>
        /// Use a dynamic object as a key so it makes it nearly impossible for consumers
        /// to grab the value without going through our service
        /// </summary>
        private readonly object _key = new object();

        IVimBufferCoordinator IVimBufferCoordinatorFactory.GetVimBufferCoordinator(IVimBuffer buffer)
        {
            return buffer.Properties.GetOrCreateSingletonProperty(
                _key,
                () => new VimBufferCoordinator(buffer));
        }
    }

}
