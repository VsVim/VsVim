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
using Vim;
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
        private readonly Microsoft.VisualStudio.OLE.Interop.IServiceProvider _sp;
        private readonly _DTE _dte;
        private readonly IUndoHistoryRegistry _undoRegistry;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        internal VsVimHost(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp, IUndoHistoryRegistry undoRegistry)
        {
            _sp = sp;
            _dte = _sp.GetService<SDTE, _DTE>();
            _undoRegistry = undoRegistry;
        }

        #region IVimHost

        void IVimHost.Beep()
        {
            Console.Beep();
        }

        void IVimHost.OpenFile(string file)
        {
            var names = _dte.GetProjects().SelectMany(x => x.GetProjecItems()).Select(x => x.Name).ToList();
            var list = _dte.GetProjectItems(file);
            
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
            _dte.StatusBar.Text = status;
        }

        void IVimHost.Undo(ITextBuffer buffer, int count)
        {
            UndoHistory history;
            if (!_undoRegistry.TryGetHistory(buffer, out history))
            {
                _dte.StatusBar.Text = "No undo possible for this buffer";
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
                    _dte.StatusBar.Text = "Undo not supported by this buffer";
                }
            }
        }

        bool IVimHost.GoToDefinition()
        {
            try
            {
                _dte.ExecuteCommand("Edit.GoToDefinition");
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
