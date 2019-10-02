using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Core;

namespace Vim.Mac
{
    [Export]
    [Export(typeof(ICommandHandler))]
    [ContentType(VimConstants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("Key Processor Provider")]
    internal class BypassKeyProcessorProvider :
            IChainedCommandHandler<TypeCharCommandArgs>,
            IChainedCommandHandler<ReturnKeyCommandArgs>,
            IChainedCommandHandler<TabKeyCommandArgs>,
            IChainedCommandHandler<BackspaceKeyCommandArgs>,
            IChainedCommandHandler<DeleteKeyCommandArgs>,
            IChainedCommandHandler<PageDownKeyCommandArgs>,
            IChainedCommandHandler<EscapeKeyCommandArgs>,
            IChainedCommandHandler<LeftKeyCommandArgs>,
            IChainedCommandHandler<RightKeyCommandArgs>,
            IChainedCommandHandler<UpKeyCommandArgs>,
            IChainedCommandHandler<DownKeyCommandArgs>
    {
        private IVim _vim;

        private readonly CommandState BypassCommandState = new CommandState(false, false, false, false);
        [ImportingConstructor]
        internal BypassKeyProcessorProvider(IVim vim)
        {
            _vim = vim;
        }

        public void ExecuteCommand(TypeCharCommandArgs args, Action nextCommandHandler, CommandExecutionContext context)
        {
            VimTrace.TraceDebug("Typed char next command");
            nextCommandHandler();
        }

        private CommandState CommonGetCommandState(EditorCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            var handled = (bool)args.TextView.Properties["Handled"];
            LoggingService.LogDebug(handled.ToString());
            if (handled)
            {
                return BypassCommandState;
            }
            return nextCommandHandler();
        }

        public CommandState GetCommandState(TypeCharCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public CommandState GetCommandState(ReturnKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            // Bypass <ret>
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(ReturnKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            var handled = (bool)args.TextView.Properties["Handled"];
            {
                LoggingService.LogDebug(handled.ToString());
                if (!handled)
                {
                    nextCommandHandler();
                }
            }
        }

        public CommandState GetCommandState(TabKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(TabKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(BackspaceKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(BackspaceKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(DeleteKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            // Bypass <C-d>
            // Unfortunately, this skips Delete key processing too
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(DeleteKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(EscapeKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(EscapeKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {

        }

        public CommandState GetCommandState(PageDownKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            // Bypass <C-v>
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(PageDownKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
            //nextCommandHandler();
        }

        public CommandState GetCommandState(LeftKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(LeftKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(RightKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(RightKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(UpKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(UpKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public CommandState GetCommandState(DownKeyCommandArgs args, Func<CommandState> nextCommandHandler)
        {
            return CommonGetCommandState(args, nextCommandHandler);
        }

        public void ExecuteCommand(DownKeyCommandArgs args, Action nextCommandHandler, CommandExecutionContext executionContext)
        {
        }

        public string DisplayName => "VsVim key handler";
    }
}

