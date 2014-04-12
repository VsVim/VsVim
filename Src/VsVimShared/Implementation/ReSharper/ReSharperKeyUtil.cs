using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim.Implementation.ReSharper
{
    internal sealed class ReSharperKeyUtil : KeyProcessor, ICommandTarget
    {
        private static object PropertyBagKey = new object();

        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;

        private ReSharperKeyUtil(IVimBufferCoordinator vimBufferCoordinator)
        {
            _vimBufferCoordinator = vimBufferCoordinator;
            _vimBuffer = vimBufferCoordinator.VimBuffer;
        }

        internal static ReSharperKeyUtil GetOrCreate(IVimBufferCoordinator vimBufferCoordinator)
        {
            return vimBufferCoordinator.VimBuffer.Properties.GetOrCreateSingletonProperty(PropertyBagKey, () => new ReSharperKeyUtil(vimBufferCoordinator));
        }

        public override bool IsInterestedInHandledEvents
        {
            get { return true; }
        }

        public override void PreviewKeyUp(KeyEventArgs args)
        {
            if (args.Key == Key.Escape)
            {
                // The Escape key was pressed and we are still inside of Insert mode.  This means that R# 
                // handled the key stroke to dismiss intellisense.  Leave insert mode now to complete the operation
                if (_vimBuffer.ModeKind == ModeKind.Insert)
                {
                    VimTrace.TraceInfo("ReSharperKeyUtil::PreviewKeyUp handled escape swallowed by Visual Assist");
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                }
            }
            base.PreviewKeyUp(args);
        }

        private bool Exec(EditCommand editCommand, out Action action)
        {
            action = null;
            return false;
        }

        private CommandStatus QueryStatus(EditCommand editCommand)
        {
            if (editCommand.HasKeyInput && _vimBuffer.CanProcess(editCommand.KeyInput))
            {
                var commandStatus = QueryStatusCore(editCommand.KeyInput);
                if (commandStatus.HasValue)
                {
                    return commandStatus.Value;
                }
            }

            return CommandStatus.PassOn;
        }

        /// <summary>
        /// With ReSharper installed we need to special certain keys like Escape.  They need to 
        /// process it in order for them to dismiss their custom intellisense but their processing 
        /// will swallow the event and not propagate it to us.  So handle, return and account 
        /// for the double stroke in exec
        /// </summary>
        private CommandStatus? QueryStatusCore(KeyInput keyInput)
        {
            CommandStatus? status = null;
            var passToResharper = true;
            if (_vimBuffer.ModeKind.IsAnyInsert() && keyInput == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for insert mode.  R# is typically ahead of us on the IOleCommandTarget
                // chain.  If a completion window is open and we wait for Exec to run R# will be ahead of us and run
                // their Exec call.  This will lead to them closing the completion window and not calling back into
                // our exec leaving us in insert mode.
                status = CommandStatus.Enable;
            }
            else if (_vimBuffer.ModeKind == ModeKind.ExternalEdit && keyInput == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for external edit mode because we want escape to get us back to 
                // normal mode.  However we do want this key to make it to R# as well since they may need to dismiss
                // intellisense
                status = CommandStatus.Enable;
            }
            else if ((keyInput.Key == VimKey.Back || keyInput == KeyInputUtil.EnterKey) && _vimBuffer.ModeKind != ModeKind.Insert)
            {
                // R# special cases both the Back and Enter command in various scenarios
                //
                //  - Enter is special cased in XML doc comments presumably to do custom formatting 
                //  - Enter is suppressed during debugging in Exec.  Presumably this is done to avoid the annoying
                //    "Invalid ENC Edit" dialog during debugging.
                //  - Back is special cased to delete matched parens in Exec.  
                //
                // In all of these scenarios if the Enter or Back key is registered as a valid Vim
                // command we want to process it as such and prevent R# from seeing the command.  If 
                // R# is allowed to see the command they will process it often resulting in double 
                // actions
                status = CommandStatus.Enable;
                passToResharper = false;
            }

            // Only process the KeyInput if we are enabling the value.  When the value is Enabled
            // we return Enabled from QueryStatus and Visual Studio will push the KeyInput back
            // through the event chain where either of the following will happen 
            //
            //  1. R# will handle the KeyInput
            //  2. R# will not handle it, it will come back to use in Exec and we will ignore it
            //     because we mark it as silently handled
            if (status.HasValue && status.Value == CommandStatus.Enable && _vimBuffer.Process(keyInput).IsAnyHandled)
            {
                // We've broken the rules a bit by handling the command in QueryStatus and we need
                // to silently handle this command if it comes back to us again either through 
                // Exec or through the VsKeyProcessor
                _vimBufferCoordinator.Discard(keyInput);

                // If we need to cooperate with R# to handle this command go ahead and pass it on 
                // to them.  Else mark it as Disabled.
                //
                // Marking it as Disabled will cause the QueryStatus call to fail.  This means the 
                // KeyInput will be routed to the KeyProcessor chain for the ITextView eventually
                // making it to our VsKeyProcessor.  That component respects the SilentlyHandled 
                // status of KeyInput and will silently handle it
                status = passToResharper ? CommandStatus.Enable : CommandStatus.Disable;
            }

            return status;
        }

        #region ICommandTarget

        CommandStatus ICommandTarget.QueryStatus(EditCommand editCommand)
        {
            return QueryStatus(editCommand);
        }

        bool ICommandTarget.Exec(EditCommand editCommand, out Action action)
        {
            return Exec(editCommand, out action);
        }

        #endregion
    }

    [Export(typeof(ICommandTargetFactory))]
    [Name("ReSharper Command Target")]
    [Order(Before=Constants.StandardCommandTargetName)]
    internal sealed class ReSharperCommandTargetFactory : ICommandTargetFactory
    {
        private readonly IReSharperUtil _reSharperUtil;

        [ImportingConstructor]
        internal ReSharperCommandTargetFactory(IReSharperUtil reSharperUtil)
        {
            _reSharperUtil = reSharperUtil;
        }

        ICommandTarget ICommandTargetFactory.CreateCommandTarget(IOleCommandTarget nextCommandTarget, IVimBufferCoordinator vimBufferCoordinator)
        {
            if (!_reSharperUtil.IsInstalled)
            {
                return null;
            }

            return ReSharperKeyUtil.GetOrCreate(vimBufferCoordinator);
        }
    }

    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Order(Before = Constants.VisualStudioKeyProcessorName, After = Constants.VsKeyProcessorName)]
    [Export(typeof(IKeyProcessorProvider))]
    [Name("ReSharper Key Processor")]
    internal sealed class ReSharperKeyProcessorProvider : IKeyProcessorProvider
    {
        private readonly IReSharperUtil _reSharperUtil;
        private readonly IVim _vim;
        private readonly IVimBufferCoordinatorFactory _vimBufferCoordinatorFactory;

        [ImportingConstructor]
        internal ReSharperKeyProcessorProvider(IVim vim, IReSharperUtil reSharperUtil, IVimBufferCoordinatorFactory vimBufferCoordinatorFactory)
        {
            _vim = vim;
            _reSharperUtil = reSharperUtil;
            _vimBufferCoordinatorFactory = vimBufferCoordinatorFactory;
        }

        KeyProcessor IKeyProcessorProvider.GetAssociatedProcessor(IWpfTextView wpfTextView)
        {
            // Don't want to invoke the custom processor unless R# is installed on the machine
            if (!_reSharperUtil.IsInstalled)
            {
                return null;
            }

            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextView, out vimBuffer))
            {
                return null;
            }

            var vimBufferCoordinator = _vimBufferCoordinatorFactory.GetVimBufferCoordinator(vimBuffer);
            return ReSharperKeyUtil.GetOrCreate(vimBufferCoordinator);
        }
    }
}
