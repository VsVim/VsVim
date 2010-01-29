using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim
{
    public interface IVsVimFactoryService
    {
        IVimFactoryService VimFactoryService { get; }
        IVimBuffer GetOrCreateBuffer(IWpfTextView textView);
    }
}
