using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Vim;

namespace VimApp
{
    internal sealed class BufferInfo
    {
        private readonly IVimTextBuffer _vimTextBuffer;
        private string _name;

        internal string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        internal IVimTextBuffer VimTextBuffer
        {
            get { return _vimTextBuffer; }
        }

        internal BufferInfo(IVimTextBuffer vimTextBuffer, string name = "<unnamed>")
        {
            _vimTextBuffer = vimTextBuffer;
            _name = name;
        }
    }

    internal sealed class ViewInfo
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly IWpfTextViewHost _textViewHost;

        internal IWpfTextViewHost TextViewHost
        {
            get { return _textViewHost; }
        }

        internal IVimBuffer VimBuffer
        {
            get { return _vimBuffer; }
        }

        internal ViewInfo(IVimBuffer vimBuffer, IWpfTextViewHost textViewHost)
        {
            _vimBuffer = vimBuffer;
            _textViewHost = textViewHost;
        }
    }

    internal sealed class TabInfo
    {
        private readonly List<ViewInfo> _viewInfoList;
        private readonly TabItem _tabItem;

        internal IEnumerable<ViewInfo> ViewInfoList
        {
            get { return _viewInfoList; }
        }

        internal TabItem TabItem
        {
            get { return _tabItem; }
        }

        internal TabInfo(TabItem tabItem)
        {
            _tabItem = tabItem;
            _viewInfoList = new List<ViewInfo>();
        }

        internal void AddViewInfo(IVimBuffer vimBuffer, IWpfTextViewHost textViewHost)
        {
            _viewInfoList.Add(new ViewInfo(vimBuffer, textViewHost));
        }
    }

    internal sealed class AppInfo
    {
        private readonly Dictionary<TabItem, TabInfo> _tabMap = new Dictionary<TabItem, TabInfo>();

        internal int Count
        {
            get { return _tabMap.Count; }
        }

        internal TabInfo GetTabInfo(TabItem tabItem)
        {
            return _tabMap[tabItem];
        }

        internal TabInfo GetOrCreateTabInfo(TabItem tabItem)
        {
            TabInfo tabInfo;
            if (_tabMap.TryGetValue(tabItem, out tabInfo))
            {
                return tabInfo;
            }

            tabInfo = new TabInfo(tabItem);
            _tabMap[tabItem] = tabInfo;
            return tabInfo;
        }
    }
}
