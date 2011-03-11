using System;
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
        internal bool TryGetSimpleMappedKeyInput(KeyRemapMode mode, KeyInput original, out KeyInput mapped)
        {
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
                var set = ((KeyMappingResult.Mapped) result).Item;
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
        /// Determine if we should process the KeyInput value.  In an ideal world this would be as simple as 
        /// calling IVimBuffer.CanProcess but Visual Studio is not a simple world.  In many cases we need to 
        /// force the command to go through IOleCommandTarget instead of going through IVimBuffer
        /// </summary>
        internal bool CanProcess(KeyInput keyInput)
        {
            // If IVimBuffer can't handle the KeyInput then definitely don't handle it 
            if (!_buffer.CanProcess(keyInput))
            {
                return false;
            }

            // If we're currently in the middle of a key mapping sequence don't filter.  Let the mapping
            // get processed
            if (!_buffer.BufferedRemapKeyInputs.IsEmpty)
            {
                return true;
            }

            if (_buffer.ModeKind == ModeKind.Insert && !CanProcess(_buffer.InsertMode, keyInput))
            {
                return false;
            }

            if (_buffer.ModeKind == ModeKind.Replace && !CanProcess(_buffer.ReplaceMode, keyInput))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Determine if the IInsertMode value should process the given KeyInput
        /// </summary>
        internal bool CanProcess(IInsertMode mode, KeyInput originalKeyInput)
        {
            // The next item we need to consider here are Key mappings.  The CanProcess and Process APIs 
            // will automatically map the KeyInput under the hood.  When considering what to filter
            // out here we want to consider the final KeyInput value not the original KeyInput. 
            KeyInput keyInput;
            if (!TryGetSimpleMappedKeyInput(KeyRemapMode.Insert, originalKeyInput, out keyInput))
            {
                // Not a simple mapping.  Don't get in the way.  Let Vim handle it.
                return true;
            }

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

        internal bool TryProcess(KeyInput keyInput)
        {
            return CanProcess(keyInput) && _buffer.Process(keyInput);
        }

        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput kiOutput)
        {
            kiOutput = null;

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

            kiOutput = command.KeyInput;
            return true;
        }

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            try
            {
                KeyInput ki;
                if (TryConvert(commandGroup, commandId, pvaIn, out ki))
                {
                    // Swallow the input if it's been flagged by a previous QueryStatus
                    if (SwallowIfNextExecMatches.IsSome && SwallowIfNextExecMatches.Value == ki)
                    {
                        return NativeMethods.S_OK;
                    }

                    if (TryProcess(ki))
                    {
                        return NativeMethods.S_OK;
                    }
                }
            }
            finally
            {
                SwallowIfNextExecMatches = Option.None;
            }

            return _nextTarget.Exec(commandGroup, commandId, nCmdexecopt, pvaIn, pvaOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            KeyInput ki;
            if (1 == cCmds && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out ki) && CanProcess(ki))
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
