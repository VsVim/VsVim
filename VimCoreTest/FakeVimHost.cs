using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text;
using System.ComponentModel.Composition;

namespace VimCoreTest
{
    [Export(typeof(IVimHost))]
    internal sealed class FakeVimHost : IVimHost
    {
        public int BeepCount { get; set; }
        public string LastFileOpen { get; set; }
        public int UndoCount { get; set; }
        public int RedoCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public int GoToMatchCount { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public VirtualSnapshotPoint NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }
        public int ShowOpenFileDialogCount { get; set; }

        [ImportingConstructor]
        public FakeVimHost()
        {
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
        }

        void IVimHost.Beep()
        {
            BeepCount++;
        }


        void IVimHost.OpenFile(string p)
        {
            LastFileOpen = p;
        }

        void IVimHost.Undo(ITextBuffer buffer, int count)
        {
            UndoCount += count;
        }

        bool IVimHost.GoToDefinition()
        {
            GoToDefinitionCount++;
            return GoToDefinitionReturn;
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            NavigateToData = point;
            return NavigateToReturn;
        }

        void IVimHost.Redo(ITextBuffer value, int count)
        {
            RedoCount += count;
        }

        string IVimHost.GetName(ITextBuffer textBuffer)
        {
            return String.Empty;
        }

        void IVimHost.ShowOpenFileDialog()
        {
            ShowOpenFileDialogCount++;
        }

        bool IVimHost.GoToMatch()
        {
            GoToMatchCount++;
            return true;
        }


        void IVimHost.Save()
        {
            throw new NotImplementedException();
        }

        void IVimHost.SaveAs(string value)
        {
            throw new NotImplementedException();
        }
    }
}
