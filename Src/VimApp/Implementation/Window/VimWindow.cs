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
        private readonly IVim _vim;
        private readonly TabItem _tabItem;
        private readonly List<VimViewInfo> _vimViewInfoList = new List<VimViewInfo>();
        private event EventHandler _changed;

        internal VimWindow(IVim vim, TabItem tabItem)
        {
            _vim = vim;
            _tabItem = tabItem;
        }

        private void RaiseChanged()
        {
            var handlers = _changed;
            if (handlers != null)
            {
                handlers(this, EventArgs.Empty);
            }
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            int i = 0;
            while (i < _vimViewInfoList.Count)
            {
                if (_vimViewInfoList[i].TextView.IsClosed)
                {
                    _vimViewInfoList.RemoveAt(i);
                }
                else
                {
                    i++;
                }
            }

            RaiseChanged();
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

        event EventHandler IVimWindow.Changed
        {
            add { _changed += value; }
            remove { _changed -= value; }
        }

        IVimViewInfo IVimWindow.AddVimViewInfo(IWpfTextViewHost textViewHost)
        {
            var vimBuffer = _vim.GetOrCreateVimBuffer(textViewHost.TextView);
            var vimViewInfo = new VimViewInfo() { VimBuffer = vimBuffer, TextViewHost = textViewHost, VimWindow = this };
            _vimViewInfoList.Add(vimViewInfo);
            textViewHost.TextView.Closed += OnTextViewClosed;
            RaiseChanged();
            return vimViewInfo;
        }

        void IVimWindow.Clear()
        {
            _vimViewInfoList.Clear();
            RaiseChanged();
        }

        #endregion
    }
}
