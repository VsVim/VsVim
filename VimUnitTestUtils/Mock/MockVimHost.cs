using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UnitTest.Mock
{
    public class MockVimHost : IVimHost
    {
        public int BeepCount { get; set; }
        public string LastFileOpen { get; set; }
        public int GoToDefinitionCount { get; set; }
        public int GoToMatchCount { get; set; }
        public bool GoToFileReturn { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public VirtualSnapshotPoint NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }
        public int ShowOpenFileDialogCount { get; set; }

        public MockVimHost()
        {
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
        }

        void IVimHost.Beep()
        {
            BeepCount++;
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

        bool IVimHost.Save(ITextView textView)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.SaveAllFiles()
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

        void IVimHost.BuildSolution()
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewDown(ITextView textView)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewUp(ITextView textView)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.GoToFile(string value)
        {
            LastFileOpen = value;
            return GoToFileReturn;
        }

        bool IVimHost.GoToGlobalDeclaration(ITextView value, string target)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.GoToLocalDeclaration(ITextView value, string target)
        {
            throw new NotImplementedException();
        }

        void IVimHost.FormatLines(ITextView value, SnapshotLineRange range)
        {
            throw new NotImplementedException();
        }

        public void EnsureVisible(ITextView textView, SnapshotPoint point)
        {
        }
    }
}
