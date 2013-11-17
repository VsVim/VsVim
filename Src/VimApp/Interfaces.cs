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

        IWpfTextView TextView { get; }

        IVimBuffer VimBuffer { get; }

        IVimWindow VimWindow { get; }
    }

    /// <summary>
    /// This represents an individual window / tab in vim
    /// </summary>
    interface IVimWindow
    {
        TabItem TabItem { get; }

        ReadOnlyCollection<IVimViewInfo> VimViewInfoList { get; }

        /// <summary>
        /// Raised when the view changes
        /// </summary>
        event EventHandler Changed;

        IVimViewInfo AddVimViewInfo(IWpfTextViewHost textViewHost);

        /// <summary>
        /// Remove all of the existing IVimViewInfo associated with this IVimWindow
        /// </summary>
        void Clear();
    }

    internal sealed class VimWindowEventArgs : EventArgs
    {
        private readonly IVimWindow _vimWindow;

        internal IVimWindow VimWindow
        {
            get { return _vimWindow; }
        }

        internal VimWindowEventArgs(IVimWindow vimWindow)
        {
            _vimWindow = vimWindow;
        }
    }

    interface IVimWindowManager
    {
        ReadOnlyCollection<IVimWindow> VimWindowList { get; }

        event EventHandler<VimWindowEventArgs> VimWindowCreated;

        IVimWindow GetVimWindow(TabItem tabItem);

        IVimWindow CreateVimWindow(TabItem tabItem);
    }
}
