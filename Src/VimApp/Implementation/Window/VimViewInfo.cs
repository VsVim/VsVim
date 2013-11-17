using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VimApp.Implementation.Window
{
    internal sealed class VimViewInfo : IVimViewInfo
    {
        internal IWpfTextViewHost TextViewHost { get; set; }
        internal IVimBuffer VimBuffer { get; set; } 

        #region IVimViewInfo

        IWpfTextViewHost IVimViewInfo.TextViewHost
        {
            get { return TextViewHost; }
        }

        IVimBuffer IVimViewInfo.VimBuffer
        {
            get { return VimBuffer; }
        }

        #endregion
    }
}
