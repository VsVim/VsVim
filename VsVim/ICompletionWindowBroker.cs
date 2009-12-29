using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    /// <summary>
    /// Represents the Vim sense of windows which are involved in completion.  It essentially 
    /// aggregates the results of intellisense and signature info.  
    /// </summary>
    public interface ICompletionWindowBroker
    {
        bool IsCompletionWindowActive(ITextView view);
        void DismissCompletionWindow(ITextView view);
    }
}
