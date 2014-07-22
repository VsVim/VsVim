using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
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
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace Vim.VisualStudio
{
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
        private readonly IVsAdapter _vsAdapter;
        private readonly IDisplayWindowBroker _broker;
        private readonly IKeyUtil _keyUtil;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private ReadOnlyCollection<ICommandTarget> _commandTargets;
        private IOleCommandTarget _nextCommandTarget;

        private VsCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            ITextManager textManager,
            IVsAdapter vsAdapter,
            IDisplayWindowBroker broker,
            IKeyUtil keyUtil,
            IVimApplicationSettings vimApplicationSettings)
        {
            _vimBuffer = vimBufferCoordinator.VimBuffer;
            _vim = _vimBuffer.Vim;
            _vimBufferCoordinator = vimBufferCoordinator;
            _textBuffer = _vimBuffer.TextBuffer;
            _vsAdapter = vsAdapter;
            _broker = broker;
            _keyUtil = keyUtil;
            _vimApplicationSettings = vimApplicationSettings;
        }

        internal VsCommandTarget(
            IVimBufferCoordinator vimBufferCoordinator,
            ITextManager textManager,
            IVsAdapter vsAdapter,
            IDisplayWindowBroker broker,
            IKeyUtil keyUtil,
            IVimApplicationSettings vimApplicationSettings,
            IOleCommandTarget nextTarget,
            ReadOnlyCollection<ICommandTarget> commandTargets)
            : this(vimBufferCoordinator, textManager, vsAdapter, broker, keyUtil, vimApplicationSettings)
        {
            CompleteInit(nextTarget, commandTargets);
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

                if (!_vimApplicationSettings.UseEditorTabAndBackspace && (command.IsBack || command.IsInsertTab))
                {
                    // When the user has opted into 'softtabstop' then Vim has a better understanding of
                    // <BS> than Visual Studio.  Allow that processing to win
                    return false;
                }

                if (!TryGetOleCommandData(command, out oleCommandData))
                {
                    // Not a command that we custom process
                    return false;
                }

                var versionNumber = _textBuffer.CurrentSnapshot.Version.VersionNumber;
                int hr = _nextCommandTarget.Exec(oleCommandData);

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

            if (command.IsInsert)
            {
                var insert = (InsertCommand.Insert)command;
                if (insert.Item != null && insert.Item.Length == 1)
                {
                    commandData = OleCommandData.CreateTypeChar(insert.Item[0]);
                    return true;
                }
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

        private void CompleteInit(IOleCommandTarget nextCommandTarget, ReadOnlyCollection<ICommandTarget> commandTargets)
        {
            Debug.Assert(_nextCommandTarget == null);
            Debug.Assert(_commandTargets == null);

            _nextCommandTarget = nextCommandTarget;
            _commandTargets = commandTargets;

            // Register the VsCommandTarget with the ITextView so that it can be retrieved later
            _vimBufferCoordinator.VimBuffer.TextView.Properties[Key] = this;
        }

        internal static Result<VsCommandTarget> Create(
            IVimBufferCoordinator vimBufferCoordinator,
            IVsTextView vsTextView,
            ITextManager textManager,
            IVsAdapter adapter,
            IDisplayWindowBroker broker,
            IKeyUtil keyUtil,
            IVimApplicationSettings vimApplicationSettings,
            ReadOnlyCollection<ICommandTargetFactory> commandTargetFactoryList)
        {
            var vsCommandTarget = new VsCommandTarget(vimBufferCoordinator, textManager, adapter, broker, keyUtil, vimApplicationSettings);

            IOleCommandTarget nextCommandTarget;
            var hresult = vsTextView.AddCommandFilter(vsCommandTarget, out nextCommandTarget);
            if (ErrorHandler.Failed(hresult))
            {
                return Result.CreateError(hresult);
            }

            var commandTargets = commandTargetFactoryList
                .Select(x => x.CreateCommandTarget(nextCommandTarget, vimBufferCoordinator))
                .Where(x => x != null)
                .ToReadOnlyCollection();
            vsCommandTarget.CompleteInit(nextCommandTarget, commandTargets);
            return Result.CreateSuccess(vsCommandTarget);
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

                return _nextCommandTarget.Exec(commandGroup, commandId, commandExecOpt, variantIn, variantOut);
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
                        return _nextCommandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
                }
            }

            return _nextCommandTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion
    }
}
