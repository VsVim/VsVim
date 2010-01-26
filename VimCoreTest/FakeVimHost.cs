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
        public string Status { get; set; }
        public List<String> LongStatus { get; set; }
        public int UndoCount { get; set; }
        public int RedoCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public Tuple<string, int, int> NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }

        [ImportingConstructor]
        public FakeVimHost()
        {
            Status = String.Empty;
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

        void IVimHost.UpdateLongStatus(IEnumerable<string> value)
        {
            LongStatus = value.ToList();
        }


        bool IVimHost.NavigateTo(string fileName, int line, int column)
        {
            NavigateToData = Tuple.Create(fileName, line, column);
            return NavigateToReturn;
        }

        void IVimHost.Redo(ITextBuffer value, int count)
        {
            RedoCount += count;
        }
    }
}
