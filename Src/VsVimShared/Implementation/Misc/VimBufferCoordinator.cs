using System.ComponentModel.Composition;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;

namespace VsVim.Implementation.Misc
{
    /// <summary>
    /// On of the main goals of this type is to coordinate the discarding of KeyInput values as 
    /// they propagate through Visual Studio.  VsVim is often competing for key strokes with other
    /// extensions like ReSharper or Visual Assist.  In many cases we can completely handle the 
    /// input ourselves and do so.  In other cases there needs to be cooperative handling.
    /// 
    /// The biggest case for this is Escape and ReSharper.  There is no way to detect if ReSharper 
    /// has intellisense displayed nor is there a way to escape it.  If it is active and they
    /// get the Escape keystroke we will never see it in IOleCommandTarget.  If we intercept it 
    /// earlier and process it then it won't be dismissed (will need double escape to leave 
    /// insert mode).  
    ///
    /// The coordinator is our solution here.  We intercept the event much earlier on in say
    /// PreviewKeyDown.  If we ever see the KeyInput again then we pretend like we already 
    /// handled it.  Because if it made it back to us a) we already handled it and b) we 
    /// don't want to be giving it to anyone else to handle
    /// </summary>
    internal sealed class VimBufferCoordinator : KeyProcessor, IVimBufferCoordinator
    {
        private readonly IVimBuffer _buffer;
        private KeyInput _discardedKeyInputOpt;

        internal VimBufferCoordinator(IVimBuffer buffer)
        {
            _buffer = buffer;
        }

        public override bool IsInterestedInHandledEvents
        {
            get { return true; }
        }

        public override void KeyUp(KeyEventArgs args)
        {
            VimTrace.TraceInfo("VimBufferCoordinator::KeyUp {0}", args.Key);
            base.KeyUp(args);
            _discardedKeyInputOpt = null;
        }

        public override void PreviewKeyUp(KeyEventArgs args)
        {
            VimTrace.TraceInfo("VimBufferCoordinator::PreviewKeyUp {0}", args.Key);
            base.PreviewKeyUp(args);
            _discardedKeyInputOpt = null;
        }

        #region IVimBufferCoordinator

        IVimBuffer IVimBufferCoordinator.VimBuffer
        {
            get { return _buffer; }
        }

        bool IVimBufferCoordinator.HasDiscardedKeyInput
        {
            get { return _discardedKeyInputOpt != null; }
        }

        bool IVimBufferCoordinator.IsDiscarded(KeyInput keyInput)
        {
            return _discardedKeyInputOpt != null && keyInput == _discardedKeyInputOpt;
        }

        void IVimBufferCoordinator.Discard(KeyInput keyInput)
        {
            _discardedKeyInputOpt = keyInput;
        }

        #endregion
    }

    [Export(typeof(IVimBufferCoordinatorFactory))]
    [Export(typeof(IKeyProcessorProvider))]
    [Order(After = Constants.VisualStudioKeyProcessorName)]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Buffer Coordinator Key Processor")]
    internal sealed class VimBufferCoordinatorFactory : IVimBufferCoordinatorFactory, IKeyProcessorProvider
    {
        /// <summary>
        /// Use a dynamic object as a key so it makes it nearly impossible for consumers
        /// to grab the value without going through our service
        /// </summary>
        private readonly object _key = new object();

        private readonly IVim _vim;

        [ImportingConstructor]
        internal VimBufferCoordinatorFactory(IVim vim)
        {
            _vim = vim;
        }

        VimBufferCoordinator GetOrCreate(IVimBuffer vimBuffer)
        {
            return vimBuffer.Properties.GetOrCreateSingletonProperty(
                _key,
                () => new VimBufferCoordinator(vimBuffer));
        }

        IVimBufferCoordinator IVimBufferCoordinatorFactory.GetVimBufferCoordinator(IVimBuffer buffer)
        {
            return GetOrCreate(buffer);
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            IVimBuffer vimBuffer;
            if (_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return GetOrCreate(vimBuffer);
            }

            return null;
        }
    }

}
