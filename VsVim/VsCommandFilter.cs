using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using VimCore;

namespace VsVim
{
    /// <summary>
    /// This class needs to intercept commands which the core VIM engine wants to process and call into the VIM engine 
    /// directly.  It needs to be very careful to not double use commands that will be processed by the KeyProcessor.  In 
    /// general it just needs to avoid processing text input
    /// </summary>
    internal sealed class VsCommandFilter : IOleCommandTarget
    {
        private readonly IVimBuffer m_buffer;
        private readonly IVsTextView m_textView;
        private readonly IOleCommandTarget m_nextTarget;

        internal VsCommandFilter(IVimBuffer buffer, IVsTextView view)
        {
            m_buffer = buffer;
            m_textView = view;
            var hr = view.AddCommandFilter(this, out m_nextTarget);
        }

        internal bool TryConvert(Guid commandGroup, uint commandId, IntPtr pvaIn, out KeyInput kiOutput)
        {
            kiOutput = null;
            EditCommand command;
            if (!CommandUtil.TryConvert(commandGroup, commandId, pvaIn, out command))
            {
                return false;
            }
            
            // If the current state of the buffer cannot process the command then do not convert it 
            if (!m_buffer.WillProcessInput(command.KeyInput))
            {

                // The one exception is input commands while we are in normal mode.  While normal mode does not actually
                // process the commands, it does need to prevent them from being routed to other buffers.  Eventually when
                // NormalMode is 100% implemented the vast majority of these will actually be processable commands.  Until then
                // though we need to process them and beep
                if (!(command.IsInput && ModeKind.Normal == m_buffer.ModeKind))
                {
                    return false;
                }
            }

            kiOutput = command.KeyInput;
            return true;
        }

        #region IOleCommandTarget implementation

        int IOleCommandTarget.Exec(ref Guid commandGroup, uint commandId, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            KeyInput ki =null;
            if (CommandUtil.IsDebugIgnore(commandGroup, commandId)
                || !TryConvert(commandGroup, commandId, pvaIn, out ki) 
                || !m_buffer.ProcessInput(ki) )
            {
                return m_nextTarget.Exec(commandGroup, commandId, nCmdexecopt, pvaIn, pvaOut);
            }

            return NativeMethods.S_OK;
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            KeyInput ki = null;
            if (0 == cCmds && TryConvert(pguidCmdGroup, cCmds, pCmdText, out ki) )
            {
                prgCmds[0].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED);
                return NativeMethods.S_OK;
            }

            return m_nextTarget.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion


    }
}
