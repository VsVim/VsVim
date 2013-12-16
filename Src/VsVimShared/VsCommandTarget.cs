using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace VsVim
{
    // TODO: establish who is control of tracing (need it for R# and standard?)
    // TODO: establish who is control of looking at IVimBufferCoordinator
    // TODO: Get rid of the ExecCore, QueryStatusCore names
    // TODO: mapped keys which relate to intellisense should be passed along as mapped

    internal enum CommandStatus
    {
        /// <summary>
        /// Command is enabled
        /// </summary>
        Enable,

        /// <summary>
        /// Command is disabled
        /// </summary>
        Disable,

        // TODO: need a better name here 
        /// <summary>
        /// VsVim isn't concerned about the command and it's left to the next IOleCommandTarget
        /// to determine if it's enabled or not
        /// </summary>
        PassOn,
    }

    internal interface ICommandTarget
    {
        CommandStatus QueryStatus(EditCommand editCommand);

        bool Exec(EditCommand editCommand, out Action action);
    }

    internal sealed class StandardCommandTarget : ICommandTarget
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly ITextManager _textManager;
        private readonly IDisplayWindowBroker _broker;

        internal StandardCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            ITextManager textManager,
            IDisplayWindowBroker broker)
        {
            _vimBuffer = vimBufferCoordinator.VimBuffer;
            _vimBufferCoordinator = vimBufferCoordinator;
            _textBuffer = _vimBuffer.TextBuffer;
            _textView = _vimBuffer.TextView;
            _textManager = textManager;
            _broker = broker;
        }

        /// <summary>
        /// Try and map a KeyInput to a single KeyInput value.  This will only succeed for KeyInput 
        /// values which have no mapping or map to a single KeyInput value
        /// </summary>
        private bool TryGetSingleMapping(KeyInput original, out KeyInput mapped)
        {
            var result = _vimBuffer.GetKeyInputMapping(original);
            if (result.IsNeedsMoreInput || result.IsRecursive || result.IsPartiallyMapped)
            {
                // No single mapping
                mapped = null;
                return false;
            }

            if (result.IsMapped)
            {
                var set = ((KeyMappingResult.Mapped)result).Item;
                if (!set.IsOneKeyInput)
                {
                    mapped = null;
                    return false;
                }

                mapped = set.FirstKeyInput.Value;
                return true;
            }

            // Shouldn't get here because all cases of KeyMappingResult should be
            // handled above
            Contract.Assert(false);
            mapped = null;
            return false;
        }

        /// <summary>
        /// Is this KeyInput intended to be processed by the active display window
        /// </summary>
        private bool IsDisplayWindowKey(KeyInput keyInput)
        {
            // Consider normal completion
            if (_broker.IsCompletionActive)
            {
                return
                    keyInput.IsArrowKey ||
                    keyInput == KeyInputUtil.EnterKey ||
                    keyInput == KeyInputUtil.TabKey ||
                    keyInput.Key == VimKey.Back;
            }

            if (_broker.IsSmartTagSessionActive)
            {
                return
                    keyInput.IsArrowKey ||
                    keyInput == KeyInputUtil.EnterKey;
            }

            if (_broker.IsSignatureHelpActive)
            {
                return keyInput.IsArrowKey;
            }

            return false;
        }

        /// <summary>
        /// Try and process the KeyInput from the Exec method.  This method decides whether or not
        /// a key should be processed directly by IVimBuffer or if should be going through 
        /// IOleCommandTarget.  Generally the key is processed by IVimBuffer but for many intellisense
        /// scenarios we want the key to be routed to Visual Studio directly.  Issues to consider 
        /// here are ...
        /// 
        ///  - How should the KeyInput participate in Macro playback?
        ///  - Does both VsVim and Visual Studio need to process the key (Escape mainly)
        ///  
        /// </summary>
        private bool TryProcessWithBuffer(KeyInput keyInput)
        {
            // If the IVimBuffer can't process it then it doesn't matter
            if (!_vimBuffer.CanProcess(keyInput))
            {
                return false;
            }

            // In the middle of a word completion session let insert mode handle the input.  It's 
            // displaying the intellisense itself and this method is meant to let custom intellisense
            // operate normally
            if (_vimBuffer.ModeKind == ModeKind.Insert && _vimBuffer.InsertMode.ActiveWordCompletionSession.IsSome())
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // The only time we actively intercept keys and route them through IOleCommandTarget
            // is when one of the IDisplayWindowBroker windows is active
            //
            // In those cases if the KeyInput is a command which should be handled by the
            // display window we route it through IOleCommandTarget to get the proper 
            // experience for those features
            if (!_broker.IsAnyDisplayActive())
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // Next we need to consider here are Key mappings.  The CanProcess and Process APIs 
            // will automatically map the KeyInput under the hood at the IVimBuffer level but
            // not at the individual IMode.  Have to manually map here and test against the 
            // mapped KeyInput
            KeyInput mapped;
            if (!TryGetSingleMapping(keyInput, out mapped))
            {
                return _vimBuffer.Process(keyInput).IsAnyHandled;
            }

            // If the key actually being processed is a display window key and the display window
            // is active then we allow IOleCommandTarget to control the key
            if (IsDisplayWindowKey(mapped))
            {
                return false;
            }

            var handled = _vimBuffer.Process(keyInput).IsAnyHandled;

            // The Escape key should always dismiss the active completion session.  However Vim
            // itself is mostly ignorant of display windows and typically won't dismiss them
            // as part of processing Escape (one exception is insert mode).  Dismiss it here if 
            // it's still active
            if (mapped.Key == VimKey.Escape && _broker.IsAnyDisplayActive())
            {
                _broker.DismissDisplayWindows();
            }

            return handled;
        }

        /// <summary>
        /// This intercepts the Paste command in Visual Studio and tries to make it work for VsVim. This is 
        /// only possible in a subset of states like command line mode.  Otherwise we default to Visual Studio
        /// behavior
        /// </summary>
        private bool Paste()
        {
            if (_vimBuffer.ModeKind != ModeKind.Command)
            {
                return false;
            }

            try
            {
                var text = Clipboard.GetText();
                var command = _vimBuffer.CommandMode.Command;
                _vimBuffer.CommandMode.Command = command + text;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool ExecCore(EditCommand editCommand, out Action action)
        {
            VimTrace.TraceInfo("VsCommandTarget::Exec {0}", editCommand);
            action = null;

            // If the KeyInput was already handled then pretend we handled it here 
            if (editCommand.HasKeyInput && _vimBufferCoordinator.IsDiscarded(editCommand.KeyInput))
            {
                return true;
            }

            switch (editCommand.EditCommandKind)
            {
                case EditCommandKind.Undo:
                    // The user hit the undo button.  Don't attempt to map anything here and instead just 
                    // run a single Vim undo operation
                    _vimBuffer.UndoRedoOperations.Undo(1);
                    return true;

                case EditCommandKind.Redo:
                    // The user hit the redo button.  Don't attempt to map anything here and instead just 
                    // run a single Vim redo operation
                    _vimBuffer.UndoRedoOperations.Redo(1);
                    return true;

                case EditCommandKind.Paste:
                    return Paste();

                case EditCommandKind.GoToDefinition:
                    // The GoToDefinition command will often cause a selection to occur in the 
                    // buffer.  We don't want that to cause us to enter Visual Mode so clear it
                    // out.  This command can cause the active document to switch if the target
                    // of the goto def is in another file.  This file won't be registered as the
                    // active file yet so just clear out the active selections
                    action = () =>
                        {
                            _textManager.GetDocumentTextViews(DocumentLoad.RespectLazy)
                                .Where(x => !x.Selection.IsEmpty)
                                .ForEach(x => x.Selection.Clear());
                        };
                    return false;

                case EditCommandKind.Comment:
                case EditCommandKind.Uncomment:
                    // The comment / uncomment command will often induce a selection on the 
                    // editor even if there was no selection before the command was run (single line
                    // case).  
                    if (_textView.Selection.IsEmpty)
                    {
                        action = () => { _textView.Selection.Clear(); };
                    }
                    return false;

                case EditCommandKind.UserInput:
                case EditCommandKind.VisualStudioCommand:
                    if (editCommand.HasKeyInput)
                    {
                        var keyInput = editCommand.KeyInput;

                        // Discard the input if it's been flagged by a previous QueryStatus
                        if (_vimBufferCoordinator.IsDiscarded(keyInput))
                        {
                            return true;
                        }

                        // Try and process the command with the IVimBuffer
                        if (TryProcessWithBuffer(keyInput))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    Debug.Assert(false);
                    return false;
            }
        }

        private CommandStatus QueryStatusCore(EditCommand editCommand)
        {
            VimTrace.TraceInfo("VsCommandTarget::QueryStatus {0}", editCommand);

            var action = CommandStatus.PassOn;
            switch (editCommand.EditCommandKind)
            {
                case EditCommandKind.Undo:
                case EditCommandKind.Redo:
                    action = CommandStatus.Enable;
                    break;
                case EditCommandKind.Paste:
                    action = _vimBuffer.ModeKind == ModeKind.Command
                        ? CommandStatus.Enable
                        : CommandStatus.PassOn;
                    break;
                default:
                    if (editCommand.HasKeyInput && _vimBuffer.CanProcess(editCommand.KeyInput))
                    {
                        action = CommandStatus.Enable;
                    }
                    break;
            }

            VimTrace.TraceInfo("VsCommandTarget::QueryStatus ", action);
            return action;
        }

        CommandStatus ICommandTarget.QueryStatus(EditCommand editCommand)
        {
            return QueryStatusCore(editCommand);
        }

        bool ICommandTarget.Exec(EditCommand editCommand, out Action action)
        {
            return ExecCore(editCommand, out action);
        }
    }

    internal sealed class ReSharperCommandTarget : ICommandTarget
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;
        private readonly IReSharperUtil _resharperUtil;

        internal ReSharperCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            IDisplayWindowBroker broker,
            IReSharperUtil resharperUtil)
        {
            _vimBuffer = vimBufferCoordinator.VimBuffer;
            _vimBufferCoordinator = vimBufferCoordinator;
            _resharperUtil = resharperUtil;
        }

        internal bool ExecCore(EditCommand editCommand, out Action action)
        {
            action = null;
            return false;
        }

        private CommandStatus QueryStatusCore(EditCommand editCommand)
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

        CommandStatus ICommandTarget.QueryStatus(EditCommand editCommand)
        {
            return QueryStatusCore(editCommand);
        }

        bool ICommandTarget.Exec(EditCommand editCommand, out Action action)
        {
            return ExecCore(editCommand, out action);
        }
    }

    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandTarget : IOleCommandTarget
    {
        /// <summary>
        /// This is the key which is used to store VsCommandTarget instances in the ITextView
        /// property bag
        /// </summary>
        private static readonly object Key = new object();

        private readonly IVimBuffer _vimBuffer;
        private readonly IVim _vim;
        private readonly IVimBufferCoordinator _vimBufferCoordinator;
        private readonly ITextBuffer _textBuffer;
        private readonly ITextView _textView;
        private readonly ITextManager _textManager;
        private readonly IVsAdapter _vsAdapter;
        private readonly IDisplayWindowBroker _broker;
        private readonly IKeyUtil _keyUtil;
        private readonly ReadOnlyCollection<ICommandTarget> _commandTargets;
        private IOleCommandTarget _nextTarget;

        private VsCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            ITextManager textManager,
            IVsAdapter vsAdapter,
            IDisplayWindowBroker broker,
            IReSharperUtil resharperUtil,
            IKeyUtil keyUtil)
        {
            _vimBuffer = vimBufferCoordinator.VimBuffer;
            _vim = _vimBuffer.Vim;
            _vimBufferCoordinator = vimBufferCoordinator;
            _textBuffer = _vimBuffer.TextBuffer;
            _textView = _vimBuffer.TextView;
            _textManager = textManager;
            _vsAdapter = vsAdapter;
            _broker = broker;
            _keyUtil = keyUtil;

            _commandTargets = new ReadOnlyCollection<ICommandTarget>(new ICommandTarget[]
                {
                    new ReSharperCommandTarget(
                        vimBufferCoordinator,
                        broker,
                        resharperUtil),
                    new StandardCommandTarget(
                        vimBufferCoordinator,
                        textManager,
                        broker)
                });
        }

        /// <summary>
        /// Try and custom process the given InsertCommand when it's appropriate to override
        /// with Visual Studio specific behavior
        /// </summary>
        public bool TryCustomProcess(InsertCommand command)
        {
            var oleCommandData = OleCommandData.Empty;
            try
            {
                if (!TryGetOleCommandData(command, out oleCommandData))
                {
                    // Not a command that we custom process
                    return false;
                }

                if (_vim.InBulkOperation && !command.IsInsertNewLine)
                {
                    // If we are in the middle of a bulk operation we don't want to forward any
                    // input to IOleCommandTarget because it will trigger actions like displaying
                    // Intellisense.  Definitely don't want intellisense popping up during say a 
                    // repeat of a 'cw' operation or macro.
                    //
                    // The one exception to this rule though is the Enter key.  Every single language
                    // formats Enter in a special way that we absolutely want to preserve in a change
                    // or macro operation.  Go ahead and let it go through here and we'll dismiss 
                    // any intellisense which pops up as a result
                    return false;
                }

                var versionNumber = _textBuffer.CurrentSnapshot.Version.VersionNumber;
                int hr = _nextTarget.Exec(oleCommandData);

                // Whether or not an Exec succeeded is a bit of a heuristic.  IOleCommandTarget implementations like
                // C++ will return E_ABORT if Intellisense failed but the character was actually inserted into 
                // the ITextBuffer.  VsVim really only cares about the character insert.  However we must also
                // consider cases where the character successfully resulted in no action as a success
                return ErrorHandler.Succeeded(hr) || versionNumber < _textBuffer.CurrentSnapshot.Version.VersionNumber;
            }
            finally
            {
                if (oleCommandData != null)
                {
                    oleCommandData.Dispose();
                }

                if (_vim.InBulkOperation && _broker.IsCompletionActive)
                {
                    _broker.DismissDisplayWindows();
                }
            }
        }

        /// <summary>
        /// Try and convert the given insert command to an OleCommand.  This should only be done
        /// for InsertCommand values which we want to custom process
        /// </summary>
        private bool TryGetOleCommandData(InsertCommand command, out OleCommandData commandData)
        {
            if (command.IsBack)
            {
                commandData = new OleCommandData(VSConstants.VSStd2KCmdID.BACKSPACE);
                return true;
            }

            if (command.IsDelete)
            {
                commandData = new OleCommandData(VSConstants.VSStd2KCmdID.DELETE);
                return true;
            }

            if (command.IsDirectInsert)
            {
                var directInsert = (InsertCommand.DirectInsert)command;
                commandData = OleCommandData.CreateTypeChar(directInsert.Item);
                return true;
            }

            if (command.IsInsertTab)
            {
                commandData = new OleCommandData(VSConstants.VSStd2KCmdID.TAB);
                return true;
            }

            if (command.IsInsertNewLine)
            {
                commandData = new OleCommandData(VSConstants.VSStd2KCmdID.RETURN);
                return true;
            }

            commandData = OleCommandData.Empty;
            return false;
        }

        /// <summary>
        /// Try and convert the Visual Studio command to it's equivalent KeyInput
        /// </summary>
        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr variantIn, out KeyInput keyInput)
        {
            keyInput = null;

            EditCommand editCommand;
            if (!TryConvert(commandGroup, commandId, variantIn, out editCommand))
            {
                return false;
            }

            if (!editCommand.HasKeyInput)
            {
                return false;
            }

            keyInput = editCommand.KeyInput;
            return true;
        }

        /// <summary>
        /// Try and convert the Visual Studio command to it's equivalent KeyInput
        /// </summary>
        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr variantIn, out EditCommand editCommand)
        {
            editCommand = null;

            // Don't ever process a command when we are in an automation function.  Doing so will cause VsVim to 
            // intercept items like running Macros and certain wizard functionality
            if (_vsAdapter.InAutomationFunction)
            {
                return false;
            }

            // Don't intercept commands while incremental search is active.  Don't want to interfere with it
            if (_vsAdapter.IsIncrementalSearchActive(_vimBuffer.TextView))
            {
                return false;
            }

            var modifiers = _keyUtil.GetKeyModifiers(_vsAdapter.KeyboardDevice.Modifiers);
            if (!OleCommandUtil.TryConvert(commandGroup, commandId, variantIn, modifiers, out editCommand))
            {
                return false;
            }

            // Don't process Visual Studio commands.  If the key sequence is mapped to a Visual Studio command
            // then that command wins.
            if (editCommand.EditCommandKind == EditCommandKind.VisualStudioCommand)
            {
                return false;
            }

            return true;
        }

        internal bool Exec(EditCommand editCommand, out Action action)
        {
            VimTrace.TraceInfo("VsCommandTarget::Exec {0}", editCommand);
            action = null;

            // If the KeyInput was already handled then pretend we handled it here 
            if (editCommand.HasKeyInput && _vimBufferCoordinator.IsDiscarded(editCommand.KeyInput))
            {
                return true;
            }

            var result = false;
            foreach (var commandTarget in _commandTargets)
            {
                if (commandTarget.Exec(editCommand, out action))
                {
                    result = true;
                    break;
                }
            }

            return result;
        }

        private CommandStatus QueryStatus(EditCommand editCommand)
        {
            VimTrace.TraceInfo("VsCommandTarget::QueryStatus {0}", editCommand);

            var action = CommandStatus.PassOn;
            foreach (var commandTarget in _commandTargets)
            {
                action = commandTarget.QueryStatus(editCommand);
                if (action != CommandStatus.PassOn)
                {
                    break;
                }
            }

            VimTrace.TraceInfo("VsCommandTarget::QueryStatus ", action);
            return action;
        }

        internal static Result<VsCommandTarget> Create(
            IVimBufferCoordinator bufferCoordinator,
            IVsTextView vsTextView,
            ITextManager textManager,
            IVsAdapter adapter,
            IDisplayWindowBroker broker,
            IReSharperUtil resharperUtil,
            IKeyUtil keyUtil)
        {
            var vsCommandTarget = new VsCommandTarget(bufferCoordinator, textManager, adapter, broker, resharperUtil, keyUtil);
            var hresult = vsTextView.AddCommandFilter(vsCommandTarget, out vsCommandTarget._nextTarget);
            var result = Result.CreateSuccessOrError(vsCommandTarget, hresult);
            if (result.IsSuccess)
            {
                bufferCoordinator.VimBuffer.TextView.Properties[Key] = vsCommandTarget;
            }

            return result;
        }

        internal static bool TryGet(ITextView textView, out VsCommandTarget vsCommandTarget)
        {
            return textView.Properties.TryGetPropertySafe(Key, out vsCommandTarget);
        }

        #region IOleCommandTarget

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            EditCommand editCommand = null;
            Action action = null;
            try
            {
                if (TryConvert(commandGroup, commandId, variantIn, out editCommand) &&
                    Exec(editCommand, out action))
                {
                    return NativeMethods.S_OK;
                }

                return _nextTarget.Exec(commandGroup, commandId, commandExecOpt, variantIn, variantOut);
            }
            finally
            {
                // Run any cleanup actions specified by ExecCore 
                if (action != null)
                {
                    action();
                }
            }
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            EditCommand editCommand;
            if (1 == cCmds && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out editCommand))
            {
                var action = QueryStatus(editCommand);
                switch (action)
                {
                    case CommandStatus.Enable:
                        prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                        return NativeMethods.S_OK;
                    case CommandStatus.Disable:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return NativeMethods.S_OK;
                    case CommandStatus.PassOn:
                        return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
                }
            }

            return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion
    }
}
