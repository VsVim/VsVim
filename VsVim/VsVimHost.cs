using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Text;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell.Interop;
using VimCore;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.UI.Undo;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    internal sealed class VsVimHost : IVimHost
    {
        private readonly Microsoft.VisualStudio.OLE.Interop.IServiceProvider m_sp;
        private readonly _DTE m_dte;
        private readonly IUndoHistoryRegistry m_undoRegistry;

        internal _DTE DTE
        {
            get { return m_dte; }
        }

        internal VsVimHost(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp, IUndoHistoryRegistry undoRegistry)
        {
            m_sp = sp;
            m_dte = m_sp.GetService<SDTE, _DTE>();
            m_undoRegistry = undoRegistry;
        }

        #region IVimHost

        void IVimHost.Beep()
        {
            Console.Beep();
        }

        void IVimHost.OpenFile(string file)
        {
            var names = m_dte.GetProjects().SelectMany(x => x.GetProjecItems()).Select(x => x.Name).ToList();
            var list = m_dte.GetProjectItems(file);
            
            if (list.Any())
            {
                var item = list.First();
                var result = item.Open(EnvDTE.Constants.vsViewKindPrimary);
                result.Activate();
                return;
            }

            Console.Beep();
        }

        void IVimHost.UpdateStatus(string status)
        {
            m_dte.StatusBar.Text = status;
        }

        void IVimHost.Undo(ITextBuffer buffer, int count)
        {
            UndoHistory history;
            if (!m_undoRegistry.TryGetHistory(buffer, out history))
            {
                m_dte.StatusBar.Text = "No undo possible for this buffer";
                return;
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (history.CanUndo)
                    {
                        history.Undo(count);
                    }
                }
                catch (NotSupportedException)
                {
                    m_dte.StatusBar.Text = "Undo not supported by this buffer";
                }
            }
        }

        bool IVimHost.GoToDefinition()
        {
            try
            {
                m_dte.ExecuteCommand("Edit.GoToDefinition");
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion

    }
}
