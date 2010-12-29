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
        private readonly IExternalEditorManager _externalEditManager;
        private IOleCommandTarget _nextTarget;

        internal Option<KeyInput> SwallowIfNextExecMatches { get; set; }

        private VsCommandTarget(
            IVimBuffer buffer,
            IVsAdapter adapter,
            IExternalEditorManager externalEditorManager)
        {
            _buffer = buffer;
            _adapter = adapter;
            _externalEditManager = externalEditorManager;
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

                    if (_buffer.CanProcess(ki) && _buffer.Process(ki))
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
                // plain old insert.
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
            IExternalEditorManager externalEditorManager)
        {
            var filter = new VsCommandTarget(buffer, adapter, externalEditorManager);
            var hresult = vsTextView.AddCommandFilter(filter, out filter._nextTarget);
            return Result.CreateSuccessOrError(filter, hresult);
        }

    }
}
