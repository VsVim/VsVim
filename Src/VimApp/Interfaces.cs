using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VimApp
{
    interface IVimAppOptions
    {
        bool DisplayNewLines { get; set; } 

        event EventHandler Changed;
    }

    /// <summary>
    /// This represents an individual view in a given IVimWindow.  It's associated with a 
    /// single IWpfTextView and IVimBuffer
    /// </summary>
    interface IVimViewInfo
    {
        IWpfTextViewHost TextViewHost { get; }

        IVimBuffer VimBuffer { get; } 
    }

    /// <summary>
    /// This represents an individual window / tab in vim
    /// </summary>
    interface IVimWindow
    {
        TabItem TabItem { get; }

        ReadOnlyCollection<IVimViewInfo> VimViewInfoList { get; }

        IVimViewInfo AddVimViewInfo(IVimBuffer vimBuffer, IWpfTextViewHost textViewHost);
    }

    interface IVimWindowManager
    {
        ReadOnlyCollection<IVimWindow> VimWindowList { get; }

        IVimWindow GetVimWindow(TabItem tabItem);

        IVimWindow CreateVimWindow(TabItem tabItem);
    }
}
