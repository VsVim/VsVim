using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace Vim.Mac
{
    //[Export(typeof(IKeyProcessorProvider))]
    //[ContentType("text")]
    //[TextViewRole(PredefinedTextViewRoles.Interactive)]
    //[Name(VimConstants.MainKeyProcessorName)]
    //internal sealed class KeyProcessorProvider2 : IKeyProcessorProvider
    //{
    //    private readonly IVim _vim;
    //    //private readonly IKeyUtil _keyUtil;

    //    [ImportingConstructor]
    //    internal KeyProcessorProvider2(IVim vim)
    //    {
    //        _vim = vim;
    //    }

    //    public KeyProcessor GetAssociatedProcessor(ICocoaTextView wpfTextView)
    //    {
    //        var vimTextBuffer = _vim.GetOrCreateVimBuffer(wpfTextView);
    //        return new VimKeyProcessor(vimTextBuffer, _keyUtil);
    //    }

    //}

    /// <summary>
    /// Passes commands to the IBraceCompletionManager found in the
    /// property bag of the view.
    /// </summary>
    [Export]
    [Export(typeof(ICommandHandler))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Key Processor Provider")]
    internal class KeyProcessorProvider :
            IChainedCommandHandler<TypeCharCommandArgs>,
            IChainedCommandHandler<ReturnKeyCommandArgs>,
            IChainedCommandHandler<TabKeyCommandArgs>,
            IChainedCommandHandler<BackspaceKeyCommandArgs>,
            IChainedCommandHandler<DeleteKeyCommandArgs>,
            IChainedCommandHandler<EscapeKeyCommandArgs>
    {
        private IVim _vim;

        [ImportingConstructor]
        internal KeyProcessorProvider(IVim vim)
        {
            _vim = vim;
        }

        public void ExecuteCommand(TypeCharCommandArgs args, Action nextCommandHandler, CommandExecutionContext context)
        {
            var vimBuffer = _vim.GetOrCreateVimBuffer(args.TextView);
            var keyInput = KeyInputUtil.CharToKeyInput(args.TypedChar);
            var process = vimBuffer.Process(keyInput);
            var notHandled = process.IsNotHandled;
            //return vimBuffer.CanProcess(keyInput) && vimBuffer.Process(keyInput).IsAnyHandled;

            if (notHandled)
            {
                nextCommandHandler();
            }

        }

        public CommandState GetCommandState(TypeCharCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public CommandState GetCommandState(ReturnKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(ReturnKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {

            nextCommandHandler();
        }

        public CommandState GetCommandState(TabKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(TabKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            nextCommandHandler();
        }

        public CommandState GetCommandState(BackspaceKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(BackspaceKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {

            nextCommandHandler();
        }

        public CommandState GetCommandState(DeleteKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(DeleteKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {

            nextCommandHandler();
        }

        public CommandState GetCommandState(EscapeKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return nextCommandHandler();
        }

        public void ExecuteCommand(EscapeKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            var vimBuffer = _vim.GetOrCreateVimBuffer(args.TextView);
            var keyInput = KeyInputUtil.VimKeyToKeyInput(VimKey.Escape);
            var notHandled = vimBuffer.Process(keyInput).IsNotHandled;
            //return vimBuffer.CanProcess(keyInput) && vimBuffer.Process(keyInput).IsAnyHandled;

            if (notHandled)
            {
                nextCommandHandler();
            }
        }

        public string DisplayName => "VsVim key handler";


    }
}

