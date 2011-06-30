using System;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    /// <summary>
    /// Container for the 4 common pieces of data which are needed for an OLE
    /// command.  Makes it easy to pass them around between functions
    /// </summary>
    internal struct OleCommandData
    {
        readonly internal uint CommandId;
        readonly internal uint CommandExecOpt;
        readonly internal IntPtr VariantIn;
        readonly internal IntPtr VariantOut;

        internal OleCommandData(VSConstants.VSStd2KCmdID id)
            : this((uint)id)
        {

        }

        internal OleCommandData(
            uint commandId,
            uint commandExecOpt = 0u)
        {
            CommandId = commandId;
            CommandExecOpt = commandExecOpt;
            VariantIn = IntPtr.Zero;
            VariantOut = IntPtr.Zero;
        }

        internal OleCommandData(
            uint commandId,
            uint commandExecOpt,
            IntPtr variantIn,
            IntPtr variantOut)
        {
            CommandId = commandId;
            CommandExecOpt = commandExecOpt;
            VariantIn = variantIn;
            VariantOut = variantOut;
        }

        /// <summary>
        /// Create an OleCommandData for typing the given character.  This causes a native resource
        /// allocation and must be freed at a later time with Release
        /// </summary>
        public static OleCommandData Allocate(char c)
        {
            var variantIn = Marshal.AllocCoTaskMem(32); // size of(VARIANT), 16 may be enough
            Marshal.GetNativeVariantForObject(c, variantIn);
            return new OleCommandData(
                (uint)VSConstants.VSStd2KCmdID.TYPECHAR,
                0,
                variantIn,
                IntPtr.Zero);
        }

        /// <summary>
        /// Release the contents of the OleCommandData.  If no allocation was performed then this 
        /// will be a no-op
        ///
        /// Do no call this one OleCommandData instances that you don't own.  Calling this on 
        /// parameters created by Visual Studio for example could easily lead to memory corruption
        /// issues
        /// </summary>
        public static void Release(ref OleCommandData oleCommandData)
        {
            if (oleCommandData.VariantIn != IntPtr.Zero)
            {
                NativeMethods.VariantClear(oleCommandData.VariantIn);
                Marshal.FreeCoTaskMem(oleCommandData.VariantIn);
            }

            if (oleCommandData.VariantOut != IntPtr.Zero)
            {
                NativeMethods.VariantClear(oleCommandData.VariantOut);
                Marshal.FreeCoTaskMem(oleCommandData.VariantOut);
            }

            oleCommandData = new OleCommandData();
        }
    }

    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandTarget : IOleCommandTarget
    {
        private enum CommandAction
        {
            Enable,
            Disable,
            PassOn
        }

        private readonly IVimBuffer _buffer;
        private readonly ITextBuffer _textBuffer;
        private readonly IVsAdapter _adapter;
        private readonly IDisplayWindowBroker _broker;
        private readonly IExternalEditorManager _externalEditManager;
        private IOleCommandTarget _nextTarget;

        internal FSharpOption<KeyInput> SwallowIfNextExecMatches { get; set; }

        private VsCommandTarget(
            IVimBuffer buffer,
            IVsAdapter adapter,
            IDisplayWindowBroker broker,
            IExternalEditorManager externalEditorManager)
        {
            _buffer = buffer;
            _textBuffer = _buffer.TextBuffer;
            _adapter = adapter;
            _broker = broker;
            _externalEditManager = externalEditorManager;
        }

        /// <summary>
        /// Try and map a KeyInput to a single KeyInput value.  This will only succeed for KeyInput 
        /// values which have no mapping or map to a single KeyInput value
        /// </summary>
        private bool TryGetSingleMapping(KeyRemapMode mode, KeyInput original, out KeyInput mapped)
        {
            var result = _buffer.GetKeyInputMapping(original);
            if (result.IsNeedsMoreInput || result.IsRecursive)
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

            if (result.IsNoMapping)
            {
                // No mapping.  Use the original
                mapped = original;
                return true;
            }

            // Shouldn't get here because all cases of KeyMappingResult should be
            // handled abvoe
            Contract.Assert(false);
            mapped = null;
            return false;
        }

        /// <summary>
        /// Determine if the IInsertMode value should process the given KeyInput
        /// </summary>
        private bool CanProcessWithInsertMode(IInsertMode mode, KeyInput keyInput)
        {
            // Don't let the mode directly process anything it considers direct input.  We need this to go
            // through IOleCommandTarget in order for features like intellisense to work properly
            if (mode.IsDirectInsert(keyInput))
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
            // like Intellisense so navigation can occur
            if (isAnyArrow && (_broker.IsCompletionActive || _broker.IsQuickInfoActive || _broker.IsSignatureHelpActive || _broker.IsSmartTagSessionActive))
            {
                return false;
            }

            // Unfortunately there is no way to detect if the R# completion windows are active.  We have
            // to take the pessimistic view that they are and just not handle the input
            if (isAnyArrow && _externalEditManager.IsResharperInstalled)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try and process the KeyInput from the Exec method
        /// </summary>
        private bool TryProcessWithBuffer(ref Guid commandGroup, ref OleCommandData oleCommandData, KeyInput keyInput)
        {
            if (!_buffer.CanProcess(keyInput))
            {
                // If the IVimBuffer can't process it then it doesn't matter
                return false;
            }

            // Next we need to determine if we can process this directly or not.  The only mode 
            // we actively intercept KeyInput for is InsertMode because we need to route it
            // through IOleCommandTarget to get Intellisense and many other features.
            var mode = _buffer.ModeKind == ModeKind.Insert
                ? _buffer.InsertMode
                : null;
            if (mode == null)
            {
                return _buffer.Process(keyInput).IsAnyHandled;
            }

            // Next we need to consider here are Key mappings.  The CanProcess and Process APIs 
            // will automatically map the KeyInput under the hood at the IVimBuffer level but
            // not at the individual IMode.  Have to manually map here and test against the 
            // mapped KeyInput
            KeyInput mapped;
            if (!TryGetSingleMapping(KeyRemapMode.Insert, keyInput, out mapped) || CanProcessWithInsertMode(mode, mapped))
            {
                return _buffer.Process(keyInput).IsAnyHandled;
            }

            // We've successfully mapped the KeyInput (even if t's a no-op) and determined that
            // we don't want to process it directly if possible.  Now we try and process the 
            // potentially mapped value
            return TryProcessWithExec(ref commandGroup, ref oleCommandData, keyInput, mapped);
        }

        /// <summary>
        /// Try and process the given KeyInput for insert mode in the middle of an Exec.  This is 
        /// called for commands which can't be processed directly like edits.  We'd prefer these 
        /// go through Visual Studio's command system so items like Intellisense work properly.
        /// </summary>
        private bool TryProcessWithExec(ref Guid commandGroup, ref OleCommandData oleCommandData, KeyInput originalKeyInput, KeyInput mappedKeyInput)
        {
            var versionNumber = _textBuffer.CurrentSnapshot.Version.VersionNumber;
            int? hr = null;
            Guid mappedCommandGroup;
            OleCommandData mappedOleCommandData;
            if (originalKeyInput == mappedKeyInput)
            {
                // No changes so just use the original OleCommandData
                hr = _nextTarget.Exec(
                    ref commandGroup,
                    oleCommandData.CommandId,
                    oleCommandData.CommandExecOpt,
                    oleCommandData.VariantIn,
                    oleCommandData.VariantOut);
            }
            else if (OleCommandUtil.TryConvert(mappedKeyInput, out mappedCommandGroup, out mappedOleCommandData))
            {
                hr = _nextTarget.Exec(
                    ref mappedCommandGroup,
                    mappedOleCommandData.CommandId,
                    mappedOleCommandData.CommandExecOpt,
                    mappedOleCommandData.VariantIn,
                    mappedOleCommandData.VariantOut);
                OleCommandData.Release(ref mappedOleCommandData);
            }

            if (hr.HasValue)
            {
                // Whether or not an Exec succeeded is a bit of a heuristic.  IOleCommandTarget implementations like
                // C++ will return E_ABORT if Intellisense failed but the character was actually inserted into 
                // the ITextBuffer.  VsVim really only cares about the character insert.  However we must also
                // consider cases where the character successfully resulted in no action as a success
                var result = ErrorHandler.Succeeded(hr.Value) || versionNumber < _textBuffer.CurrentSnapshot.Version.VersionNumber;

                // We processed the input and bypassed the IVimBuffer instance.  We need to tell IVimBuffer this
                // KeyInput was processed so it can track it for macro purposes.  Make sure to track the mapped
                // KeyInput value.  The SimulateProcessed method does not do any mapping
                if (result)
                {
                    _buffer.SimulateProcessed(mappedKeyInput);
                }

                // Whether or not this succeeded it was processed to the fullest possible extent
                return true;
            }

            // If we couldn't map the KeyInput value into a Visual Studio command then go straight to the 
            // ITextBuffer.  Insert mode is already designed to handle these KeyInput values we'd just prefer
            // to pass them through Visual Studio.
            return _buffer.Process(originalKeyInput).IsAnyHandled;
        }

        /// <summary>
        /// Try and convert the Visual Studio command to it's equivalent KeyInput
        /// </summary>
        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput keyInput)
        {
            keyInput = null;

            EditCommand editCommand;
            if (!TryConvert(commandGroup, commandId, pvaIn, out editCommand))
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
        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out EditCommand editCommand)
        {
            editCommand = null;

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

            return OleCommandUtil.TryConvert(commandGroup, commandId, pvaIn, out editCommand);
        }

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint commandExecOpt, IntPtr variantIn, IntPtr variantOut)
        {
            try
            {
                EditCommand editCommand;
                if (TryConvert(commandGroup, commandId, variantIn, out editCommand))
                {
                    if (editCommand.IsUndo)
                    {
                        // The user hit the undo button.  Don't attempt to map anything here and instead just 
                        // run a single Vim undo operation
                        _buffer.UndoRedoOperations.Undo(1);
                        return NativeMethods.S_OK;
                    }
                    else if (editCommand.IsRedo)
                    {
                        // The user hit the redo button.  Don't attempt to map anything here and instead just 
                        // run a single Vim redo operation
                        _buffer.UndoRedoOperations.Redo(1);
                        return NativeMethods.S_OK;
                    }
                    else if (editCommand.HasKeyInput)
                    {
                        var keyInput = editCommand.KeyInput;

                        // Swallow the input if it's been flagged by a previous QueryStatus
                        if (SwallowIfNextExecMatches.IsSome() && SwallowIfNextExecMatches.Value == keyInput)
                        {
                            return NativeMethods.S_OK;
                        }

                        // Try and process the command with the IVimBuffer
                        var commandData = new OleCommandData(commandId, commandExecOpt, variantIn, variantOut);
                        if (TryProcessWithBuffer(ref commandGroup, ref commandData, keyInput))
                        {
                            return NativeMethods.S_OK;
                        }
                    }
                }
            }
            finally
            {
                SwallowIfNextExecMatches = FSharpOption<KeyInput>.None;
            }

            return _nextTarget.Exec(commandGroup, commandId, commandExecOpt, variantIn, variantOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            EditCommand editCommand;
            if (1 == cCmds && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out editCommand))
            {
                var action = CommandAction.PassOn;
                if (editCommand.IsUndo || editCommand.IsRedo)
                {
                    action = CommandAction.Enable;
                }
                else if (editCommand.HasKeyInput && _buffer.CanProcess(editCommand.KeyInput))
                {
                    action = CommandAction.Enable;
                    if (_externalEditManager.IsResharperInstalled)
                    {
                        action = QueryStatusInResharper(editCommand.KeyInput) ?? CommandAction.Enable;
                    }
                }

                switch (action)
                {
                    case CommandAction.Enable:
                        prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                        return NativeMethods.S_OK;
                    case CommandAction.Disable:
                        prgCmds[0].cmdf = 0;
                        return NativeMethods.S_OK;
                    case CommandAction.PassOn:
                        return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
                }
            }

            return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        /// <summary>
        /// With Resharper installed we need to special certain keys like Escape.  They need to 
        /// process it in order for them to dismiss their custom intellisense but their processing 
        /// will swallow the event and not propagate it to us.  So handle, return and account 
        /// for the double stroke in exec
        /// </summary>
        private CommandAction? QueryStatusInResharper(KeyInput keyInput)
        {
            CommandAction? action = null;
            if (_buffer.ModeKind == ModeKind.Insert && keyInput == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for insert mode.  R# is typically ahead of us on the IOleCommandTarget
                // chain.  If a completion window is open and we wait for Exec to run R# will be ahead of us and run
                // their Exec call.  This will lead to them closing the completion window and not calling back into
                // our exec leaving us in insert mode.
                action = CommandAction.Enable;
            }
            else if (_buffer.ModeKind == ModeKind.ExternalEdit && keyInput == KeyInputUtil.EscapeKey)
            {
                // Have to special case Escape here for external edit mode because we want escape to get us back to 
                // normal mode
                action = CommandAction.Enable;
            }
            else if (_adapter.InDebugMode && (keyInput == KeyInputUtil.EnterKey || keyInput.Key == VimKey.Back))
            {
                // In debug mode R# will intercept Enter and Back 
                action = CommandAction.Enable;
            }
            else if (keyInput == KeyInputUtil.EnterKey && _buffer.ModeKind != ModeKind.Insert && _buffer.ModeKind != ModeKind.Replace)
            {
                // R# will intercept the Enter key when we are in the middle of an XML doc comment presumable
                // to do some custom formatting.  If we're not insert mode we need to handle that here and 
                // suppress the command to keep them from running it
                action = CommandAction.Disable;
            }

            // Only process the KeyInput if we are enabling the value.  When the value is Enabled
            // we return Enabled from QueryStatus and Visual Studio will push the KeyInput back
            // through the event chain where either of the following will happen 
            //
            //  1. R# will handle the KeyInput
            //  2. R# will not handle it, it will get back to us and we will ignore it
            //
            // If the command is disabled though it will not go through IOleCommandTarget and instead will end 
            // up in the KeyProcessor code which will handle the value
            if (action.HasValue && action.Value == CommandAction.Enable && _buffer.Process(keyInput).IsAnyHandled)
            {
                SwallowIfNextExecMatches = FSharpOption.Create(keyInput);
            }

            return action;
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
