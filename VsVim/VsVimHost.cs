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
using Microsoft.VisualStudio.Language.Intellisense;
using System.ComponentModel.Composition;
using VsVim.Properties;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio;
using System.Windows;
using Microsoft.VisualStudio.Text.Operations;


namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    internal sealed class VsVimHost : IVimHost
    {
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly ITextBufferUndoManagerProvider _undoManagerProvider;

        /// <summary>
        /// Until we hit RC, we cannot import IServiceProvider which is where we get the DTE and other
        /// instances from.  Yet we need to provide IVimHost as a MEF component.  As a temporary work around
        /// we will leave this as a mutable field and bail out whenever it's not NULL
        /// </summary>
        private _DTE _dte;
        private IVsTextManager _textManager;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        [ImportingConstructor]
        internal VsVimHost(ITextBufferUndoManagerProvider undoManagerProvider, IVsEditorAdaptersFactoryService editorAdaptersFactoryService)
        {
            _undoManagerProvider = undoManagerProvider;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
        }

        internal void OnServiceProvider(Microsoft.VisualStudio.OLE.Interop.IServiceProvider sp)
        {
            Init(sp.GetService<SDTE, EnvDTE.DTE>(),
                _textManager = sp.GetService<SVsTextManager, IVsTextManager>());
        }

        internal void Init(
            _DTE dte,
            IVsTextManager textManager)
        {
            _dte = dte;
            _textManager = textManager;
        }

        private void UpdateStatus(string text)
        {
            if (_dte == null)
            {
                return;
            }

            _dte.StatusBar.Text = text;
        }

        private bool SafeExecuteCommand(string command)
        {
            if (_dte == null)
            {
                return false;
            }

            try
            {
                _dte.ExecuteCommand(command);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
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
            var builder = new StringBuilder();
            foreach (var item in statusLines)
            {
                builder.AppendLine(item);
            }
            MessageBox.Show(
                caption: "Vim Status Update",
                messageBoxText: builder.ToString(),
                button: MessageBoxButton.OK);
        }

        void IVimHost.Undo(ITextBuffer buffer, int count)
        {
            var undoManager = _undoManagerProvider.GetTextBufferUndoManager(buffer);
            if ( undoManager == null || undoManager.TextBufferUndoHistory == null )
            {
                UpdateStatus(Resources.VimHost_NoUndoRedoSupport);
                return;
            }

            var history = undoManager.TextBufferUndoHistory;
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
                    UpdateStatus(Resources.VimHost_CannotUndo);
                }
            }
        }

        void IVimHost.Redo(ITextBuffer buffer, int count)
        {
            var undoManager = _undoManagerProvider.GetTextBufferUndoManager(buffer);
            if (undoManager == null || undoManager.TextBufferUndoHistory == null)
            {
                UpdateStatus(Resources.VimHost_NoUndoRedoSupport);
                return;
            }

            var history = undoManager.TextBufferUndoHistory;
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
                    UpdateStatus(Resources.VimHost_CannotRedo);
                }
            }
        }

        bool IVimHost.GoToDefinition()
        {
            return SafeExecuteCommand("Edit.GoToDefinition");
        }

        void IVimHost.ShowOpenFileDialog()
        {
            SafeExecuteCommand("Edit.OpenFile");
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            if (_textManager == null)
            {
                return false;
            }

            var snapshotLine = point.Position.GetContainingLine();
            var column = point.Position.Position - snapshotLine.Start.Position;
            var vsBuffer = _editorAdaptersFactoryService.GetBufferAdapter(point.Position.Snapshot.TextBuffer);
            var viewGuid = VSConstants.LOGVIEWID_Code;
            var hr = _textManager.NavigateToLineAndColumn(
                vsBuffer,
                ref viewGuid,
                snapshotLine.LineNumber,
                column,
                snapshotLine.LineNumber,
                column);
            return ErrorHandler.Succeeded(hr);
        }

        string IVimHost.GetName(ITextBuffer buffer)
        {
            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(buffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return String.Empty;
            }
            return vsTextLines.GetFileName();
        }

        #endregion


    }
}
