using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VimCore.Test
{
    [Export(typeof(IVimHost))]
    internal sealed class FakeVimHost : IVimHost
    {
        public int BeepCount { get; set; }
        public string LastFileOpen { get; set; }
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

        void IVimHost.Close(ITextView textView, bool checkDirty)
        {
            throw new NotImplementedException();
        }

        void IVimHost.CloseAllFiles(bool checkDirty)
        {
            throw new NotImplementedException();
        }

        void IVimHost.Save(ITextView textView)
        {
            throw new NotImplementedException();
        }

        void IVimHost.SaveCurrentFileAs(string value)
        {
            throw new NotImplementedException();
        }

        void IVimHost.SaveAllFiles()
        {
            throw new NotImplementedException();
        }

        void IVimHost.GoToNextTab(int count)
        {
            throw new NotImplementedException();
        }

        void IVimHost.GoToPreviousTab(int count)
        {
            throw new NotImplementedException();
        }

        void IVimHost.SplitView(ITextView textView)
        {
            throw new NotImplementedException();
        }

        void IVimHost.CloseView(ITextView textView, bool checkDirty)
        {
            throw new NotImplementedException();
        }

        void IVimHost.BuildSolution()
        {
            throw new NotImplementedException();
        }
    }
}
