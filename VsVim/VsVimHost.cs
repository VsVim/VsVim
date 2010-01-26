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
using Microsoft.VisualStudio.Language.Intellisense;
using System.ComponentModel.Composition;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    internal sealed class VsVimHost : IVimHost
    {
        private readonly IUndoHistoryRegistry _undoRegistry;

        /// <summary>
        /// Until we hit RC, we cannot import IServiceProvider which is where we get the DTE instance
        /// from.  Yet we need to provide IVimHost as a MEF component.  As a temporary work around
        /// we will leave this as a mutable field and bail out whenever it's not NULL
        /// </summary>
        private _DTE _dte;

        internal _DTE DTE
        {
            get { return _dte; }
            set { _dte = value; }
        }

        [ImportingConstructor]
        internal VsVimHost(IUndoHistoryRegistry undoRegistry)
        {
            _undoRegistry = undoRegistry;
        }

        private void UpdateStatus(string text)
        {
            if (_dte == null)
            {
                return;
            }

            _dte.StatusBar.Text = text;
        }

        #region IVimHost

        void IVimHost.Beep()
        {
            Console.Beep();
        }

        void IVimHost.OpenFile(string file)
        {
            if (_dte == null)
            {
                return;
            }

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
            UpdateStatus(status);
        }

        void IVimHost.UpdateLongStatus(IEnumerable<string> statusLines)
        {
            // TODO: Implement a WPF popup window
        }

        void IVimHost.Undo(ITextBuffer buffer, int count)
        {
            UndoHistory history;
            if (!_undoRegistry.TryGetHistory(buffer, out history))
            {
                UpdateStatus("No undo possible for this buffer");
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
                    UpdateStatus("Undo not supported by this buffer");
                }
            }
        }

        void IVimHost.Redo(ITextBuffer buffer, int count)
        {
            UndoHistory history;
            if (!_undoRegistry.TryGetHistory(buffer, out history))
            {
                UpdateStatus("No redo possible for this buffer");
                return;
            }

            for (int i = 0; i < count; i++)
            {
                try
                {
                    if (history.CanRedo)
                    {
                        history.Redo(count);
                    }
                }
                catch (NotSupportedException)
                {
                    UpdateStatus("Redo not supported by this buffer");
                }
            }
        }

        bool IVimHost.GoToDefinition()
        {
            if (_dte == null)
            {
                return false;
            }

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

        bool IVimHost.NavigateTo(string fileName, int line, int column)
        {
            // TODO: Implement
            return false;
        }

        #endregion


    }
}
