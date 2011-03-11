using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;

namespace VsVim
{
    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandTarget : IOleCommandTarget
    {
        struct CommandData
        {
            internal uint CommandId;
            internal uint CommandExecOpt;
            internal IntPtr VariantIn;
            internal IntPtr VariantOut;
        }

        private readonly IVimBuffer _buffer;
        private readonly IVsAdapter _adapter;
        private readonly IDisplayWindowBroker _broker;
        private readonly IExternalEditorManager _externalEditManager;
        private IOleCommandTarget _nextTarget;

        internal Option<KeyInput> SwallowIfNextExecMatches { get; set; }

        private VsCommandTarget(
            IVimBuffer buffer,
            IVsAdapter adapter,
            IDisplayWindowBroker broker,
            IExternalEditorManager externalEditorManager)
        {
            _buffer = buffer;
            _adapter = adapter;
            _broker = broker;
            _externalEditManager = externalEditorManager;
        }

        /// <summary>
        /// Try and map a KeyInput to a single KeyInput value.  This will only suceed for KeyInput 
        /// values which have no mapping or map to a single KeyInput value
        /// </summary>
        private bool TryGetSingleMapping(KeyRemapMode mode, KeyInput original, out KeyInput mapped)
        {
            // If we're currently in the middle of a key mapping sequence we won't provide a KeyInput
            if (!_buffer.BufferedRemapKeyInputs.IsEmpty)
            {
                mapped = null;
                return false;
            }

            var result = _buffer.Vim.KeyMap.GetKeyMapping(
                KeyInputSet.NewOneKeyInput(original),
                mode);

            mapped = null;
            if (result.IsMappingNeedsMoreInput || result.IsRecursiveMapping)
            {
                // No single mapping
                return false;
            }
            else if (result.IsMapped)
            {
                var set = ((KeyMappingResult.Mapped)result).Item;
                if (!set.IsOneKeyInput)
                {
                    return false;
                }

                mapped = set.FirstKeyInput.Value;
                return true;
            }
            else
            {
                // No mapping.  Use the original
                mapped = original;
                return true;
            }
        }

        /// <summary>
        /// Determine if the IInsertMode value should process the given KeyInput
        /// </summary>
        private bool CanProcessDirectly(IInsertMode mode, KeyInput keyInput)
        {
            // Don't let the mode directly process anything it considers text input.  We need this to go
            // through IOleCommandTarget in order to get intellisense values.  
            if (mode.IsTextInput(keyInput))
            {
                return false;
            }

            var isAnyArrow =
                keyInput.Key == VimKey.Up ||
                keyInput.Key == VimKey.Down ||
                keyInput.Key == VimKey.Left ||
                keyInput.Key == VimKey.Right;

            // If this is any of the arrow keys and one of the help windows is active then don't 
            // let insert mode process the input.  We want the KeyInput to be routed to the windows
            // like intellisense so navigation can occur
            if (isAnyArrow && (_broker.IsCompletionActive || _broker.IsQuickInfoActive || _broker.IsSignatureHelpActive || _broker.IsSmartTagSessionActive))
            {
                return false;
            }

            // Unfortunately there is no way to detect if the R# completion windows are active.  We have
            // to take the pesimistic view that they are not and just not handle the input
            if (isAnyArrow && _externalEditManager.IsResharperLoaded)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try and process the KeyInput from the Exec method
        /// </summary>
        private bool TryExec(ref Guid commandGroup, ref CommandData commandData, KeyInput keyInput)
        {
            if (!_buffer.CanProcess(keyInput))
            {
                // If the IVimBuffer can't process it then it doesn't matter
                return false;
            }

            // Next we need to determine if we can process this directy or not.  The only mode 
            // we actively intercept KeyInput for is InsertMode because we need to route it
            // through IOleCommandTarget to get intellisense and many other features.
            var mode = _buffer.ModeKind == ModeKind.Insert
                ? _buffer.InsertMode
                : _buffer.ModeKind == ModeKind.Replace ? _buffer.ReplaceMode : null;
            if (mode == null)
            {
                return _buffer.Process(keyInput);
            }

            // Next we need to consider here are Key mappings.  The CanProcess and Process APIs 
            // will automatically map the KeyInput under the hood at the IVimBuffer level but
            // not at the individual IMode.  Have to manually map here and test against the 
            // mapped KeyInput
            KeyInput mapped;
            if (!TryGetSingleMapping(KeyRemapMode.Insert, keyInput, out mapped) || CanProcessDirectly(mode, mapped))
            {
                return _buffer.Process(keyInput);
            }

            // At this point we've determined that we need to intercept this 
            var result = TryExecIntercepted(ref commandGroup, ref commandData, keyInput, mapped);
            if (result)
            {
                // We processed the input and bypassed the IVimBuffer instance.  We need to tell IVimBuffer this
                // KeyInput was processed so it can track it for macro purposes.  Make sure to track the mapped
                // KeyInput value.  The SimulateProcessed method does not mapping
                _buffer.SimulateProcessed(mapped);
            }

            return result;
        }

        /// <summary>
        /// Try and exec this KeyInput in an intercepted fashion
        /// </summary>
        private bool TryExecIntercepted(ref Guid commandGroup, ref CommandData commandData, KeyInput originalKeyInput, KeyInput mappedKeyInput)
        {
            if (originalKeyInput == mappedKeyInput)
            {
                // No changes so just use the original CommandData
                return VSConstants.S_OK == _nextTarget.Exec(
                    ref commandGroup,
                    commandData.CommandId,
                    commandData.CommandExecOpt,
                    commandData.VariantIn,
                    commandData.VariantOut);
            }

            // TODO: Handle mapped inputs
            return false;
        }

        /// <summary>
        /// Try and convert the Visual Studio command to it's equivalent KeyInput
        /// </summary>
        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput keyInput)
        {
            keyInput = null;

            // Don't ever process a command when we are in an automation function.  Doing so will cause VsVim to 
            // intercept items like running Macros and certain wizard functionality
            if (_adapter.InAutomationFunction)
            {
                return false;
            }

            // Don't intercept commands while incremental search is active.  Don't want to interfere with it
            if (_adapter.IsIncrementalSearchActive(_buffer.TextView))
            {
                return false;
            }

            EditCommand command;
            if (!OleCommandUtil.TryConvert(commandGroup, commandId, pvaIn, out command))
            {
                return false;
            }

            keyInput = command.KeyInput;
            return true;
        }

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            try
            {
                KeyInput ki;
                if (TryConvert(commandGroup, commandId, variantIn, out ki))
                {
                    // Swallow the input if it's been flagged by a previous QueryStatus
                    if (SwallowIfNextExecMatches.IsSome && SwallowIfNextExecMatches.Value == ki)
                    {
                        return NativeMethods.S_OK;
                    }

                    var commandData = new CommandData
                    {
                        CommandId = commandId,
                        CommandExecOpt = commandExecOpt,
                        VariantIn = variantIn,
                        VariantOut = variantOut
                    };
                    if (TryExec(ref commandGroup, ref commandData, ki))
                    {
                        return NativeMethods.S_OK;
                    }
                }
            }
            finally
            {
                SwallowIfNextExecMatches = Option.None;
            }

            return _nextTarget.Exec(commandGroup, commandId, commandExecOpt, variantIn, variantOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            KeyInput ki;
            if (1 == cCmds && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out ki) && _buffer.CanProcess(ki))
            {
                if (_externalEditManager.IsResharperLoaded)
                {
                    QueryStatusInResharper(ki);
                }

                prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                return NativeMethods.S_OK;
            }

            return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        /// <summary>
        /// With Resharper installed we need to special certain keys like Escape.  They need to 
        /// process it in order for them to dismiss their custom intellisense but their processing 
        /// will swallow the event and not propagate it to us.  So handle, return and account 
        /// for the double stroke in exec
        /// </summary>
        private void QueryStatusInResharper(KeyInput ki)
        {
            var shouldHandle = false;
            if (_buffer.ModeKind == ModeKind.Insert && ki == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for insert mode.  R# is typically ahead of us on the IOleCommandTarget
                // chain.  If a completion window is open and we wait for Exec to run R# will be ahead of us and run
                // their Exec call.  This will lead to them closing the completion window and not calling back into
                // our exec leaving us in insert mode.
                shouldHandle = true;
            }
            else if (_buffer.ModeKind == ModeKind.ExternalEdit && ki == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for external edit mode because we want escape to get us back to 
                // normal mode
                shouldHandle = true;
            }
            else if (_adapter.InDebugMode && (ki == KeyInputUtil.EnterKey || ki.Key == VimKey.Back))
            {
                // In debug mode R# will intercept Enter and Back 
                shouldHandle = true;
            }

            if (shouldHandle && _buffer.Process(ki))
            {
                SwallowIfNextExecMatches = ki;
            }
        }

        internal static Result<VsCommandTarget> Create(
            IVimBuffer buffer,
            IVsTextView vsTextView,
            IVsAdapter adapter,
            IDisplayWindowBroker broker,
            IExternalEditorManager externalEditorManager)
        {
            var filter = new VsCommandTarget(buffer, adapter, broker, externalEditorManager);
            var hresult = vsTextView.AddCommandFilter(filter, out filter._nextTarget);
            return Result.CreateSuccessOrError(filter, hresult);
        }

    }
}
