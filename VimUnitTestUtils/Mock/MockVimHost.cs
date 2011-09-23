using System;
using Microsoft.FSharp.Collections;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;

namespace Vim.UnitTest.Mock
{
    public class MockVimHost : IVimHost
    {
        public int BeepCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToFileReturn { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public VirtualSnapshotPoint NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }
        public int ShowOpenFileDialogCount { get; set; }
        public ITextView FocusedTextView { get; set; }
        public FSharpList<IVimBuffer> Buffers { get; set; }

        public MockVimHost()
        {
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
        }

        /// <summary>
        /// Clear out the stored information
        /// </summary>
        public void Clear()
        {
            Buffers = FSharpList<IVimBuffer>.Empty;
            BeepCount = 0;
            GoToDefinitionCount = 0;
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

        void IVimHost.Close(ITextView textView, bool checkDirty)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.Save(ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.SplitViewHorizontally(ITextView textView)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.Make(bool jumpToFirstError, string arguments)
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

        void IVimHost.EnsureVisible(ITextView textView, SnapshotPoint value)
        {

        }

        bool IVimHost.IsDirty(ITextBuffer value)
        {
            return false;
        }

        HostResult IVimHost.LoadFileIntoExistingWindow(string filePath, ITextBuffer textBuffer)
        {
            return HostResult.Success;
        }

        bool IVimHost.Reload(ITextBuffer value)
        {
            return true;
        }


        void IVimHost.GoToNextTab(Path value, int count)
        {
            throw new NotImplementedException();
        }

        void IVimHost.GoToTab(int index)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewLeft(ITextView value)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewRight(ITextView value)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.SplitViewVertically(ITextView value)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.LoadFileIntoNewWindow(string filePath)
        {
            throw new NotImplementedException();
        }

        public Microsoft.FSharp.Core.FSharpOption<ITextView> GetFocusedTextView()
        {
            return FSharpOption.CreateForReference(FocusedTextView);
        }

        void IVimHost.Quit()
        {
            throw new NotImplementedException();
        }
    }
}
