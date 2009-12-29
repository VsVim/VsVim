using System;
using System.Collections.Generic;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text;

namespace VimCoreTest
{
    internal sealed class FakeVimHost : IVimHost
    {

        public int BeepCount { get; set; }
        public string LastFileOpen { get; set; }
        public string Status { get; set; }
        public int UndoCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }

        public FakeVimHost()
        {
            Status = String.Empty;
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
        }

        void IVimHost.Beep()
        {
            BeepCount++;
        }


        void IVimHost.OpenFile(string p)
        {
            LastFileOpen = p;
        }

        void IVimHost.UpdateStatus(string status)
        {
            Status = status;
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


        void IVimHost.DismissCompletionWindow(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            DismissCompletionWindowCount++;
        }

        bool IVimHost.IsCompletionWindowActive(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            return IsCompletionWindowActive;
        }
    }
}
