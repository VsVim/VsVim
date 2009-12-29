using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    internal interface ICompletionWindowBroker
    {
        bool IsCompletionWindowActive(ITextView view);
        void DismissCompletionWindow(ITextView view);
    }
}
