using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VimApp.Implementation.Window
{
    internal sealed class VimWindow : IVimWindow
    {
        private readonly TabItem _tabItem;
        private readonly List<VimViewInfo> _vimViewInfoList = new List<VimViewInfo>();

        internal VimWindow(TabItem tabItem)
        {
            _tabItem = tabItem;
        }

        #region IVimWindow

        TabItem IVimWindow.TabItem
        {
            get { return _tabItem; }
        }

        ReadOnlyCollection<IVimViewInfo> IVimWindow.VimViewInfoList
        {
            get { return new ReadOnlyCollection<IVimViewInfo>(_vimViewInfoList.Cast<IVimViewInfo>().ToList()); }
        }

        IVimViewInfo IVimWindow.AddVimViewInfo(IVimBuffer vimBuffer, IWpfTextViewHost textViewHost)
        {
            var vimViewInfo = new VimViewInfo() { VimBuffer = vimBuffer, TextViewHost = textViewHost };
            _vimViewInfoList.Add(vimViewInfo);
            return vimViewInfo;
        }

        #endregion

    }
}
