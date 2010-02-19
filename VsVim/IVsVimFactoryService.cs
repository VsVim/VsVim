using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;

namespace VsVim
{
    public interface IVsVimFactoryService
    {
        IVimFactoryService VimFactoryService { get; }
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider GetOrUpdateServiceProvider(ITextBuffer buffer);
    }
}
