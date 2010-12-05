using System;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using IServiceProvider = System.IServiceProvider;

namespace VsVim
{
    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandFilter : IOleCommandTarget
    {
        private readonly IVimBuffer _buffer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IExternalEditorManager _externalEditManager;
        private IOleCommandTarget _nextTarget;

        private VsCommandFilter(
            IVimBuffer buffer,
            IServiceProvider provider,
            IExternalEditorManager externalEditorManager)
        {
            _buffer = buffer;
            _serviceProvider = provider;
            _externalEditManager = externalEditorManager;
        }

        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput kiOutput)
        {
            kiOutput = null;

            // Don't ever process a command when we are in an automation function.  Doing so will cause VsVim to 
            // intercept items like running Macros and certain wizard functionality
            if (VsShellUtilities.IsInAutomationFunction(_serviceProvider))
            {
                return false;
            }

            EditCommand command;
            if (!OleCommandUtil.TryConvert(commandGroup, commandId, pvaIn, out command))
            {
                return false;
            }

            // If the current state of the buffer cannot process the command then do not convert it 
            if (!_buffer.CanProcess(command.KeyInput))
            {
                return false;
            }

            kiOutput = command.KeyInput;
            return true;
        }

        #region IOleCommandTarget implementation

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            KeyInput ki;
            if (TryConvert(commandGroup, commandId, pvaIn, out ki) && _buffer.Process(ki))
            {
                return NativeMethods.S_OK;
            }

            return _nextTarget.Exec(commandGroup, commandId, nCmdexecopt, pvaIn, pvaOut);
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            KeyInput ki;
            if (1 == cCmds && TryConvert(pguidCmdGroup, prgCmds[0].cmdID, pCmdText, out ki))
            {
                var handled = false;
                if (_externalEditManager.IsResharperLoaded)
                {
                    // TODO: Need to change this logic so that R# actually gets the keystroke.  Could process it here
                    // return that the command is enabled and then ignore it if it comes right back to us in exec

                    if (_buffer.ModeKind == ModeKind.Insert && ki.Key == VimKey.Escape && _buffer.Process(ki))
                    {
                        // Have to special case Escape here for insert mode.  R# is typically ahead of us on the IOleCommandTarget
                        // chain.  If a completion window is open and we wait for Exec to run R# will be ahead of us and run
                        // their Exec call.  This will lead to them closing the completion window and not calling back into
                        // our exec leaving us in insert mode.
                        handled = true;
                    }
                    else if (_buffer.ModeKind == ModeKind.ExternalEdit && ki.Key == VimKey.Escape && _buffer.Process(ki))
                    {
                        // Have to special case Escape here for external edit mode because we want escape to get us back to 
                        // plain old insert.
                        handled = true;
                    }
                }

                if (handled)
                {
                    prgCmds[0].cmdf = 0;
                }
                else
                {
                    prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                }

                return NativeMethods.S_OK;
            }

            return _nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion

        internal static Result<VsCommandFilter> Create(
            IVimBuffer buffer,
            IVsTextView vsTextView,
            IServiceProvider serviceProvider,
            IExternalEditorManager externalEditorManager)
        {
            var filter = new VsCommandFilter(buffer, serviceProvider, externalEditorManager);
            var hresult= vsTextView.AddCommandFilter(filter, out filter._nextTarget);
            return Result.CreateValueOrError(filter, hresult);
        }

    }
}
