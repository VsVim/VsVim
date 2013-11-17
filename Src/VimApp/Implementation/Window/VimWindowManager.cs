using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using Vim;

namespace VimApp.Implementation.Window
{
    [Export(typeof(IVimWindowManager))]
    internal sealed class VimWindowManager : IVimWindowManager
    {
        private readonly Dictionary<TabItem, VimWindow> _map = new Dictionary<TabItem, VimWindow>();
        private readonly IVim _vim;
        private EventHandler<VimWindowEventArgs> _vimWindowCreated;

        [ImportingConstructor]
        internal VimWindowManager(IVim vim)
        {
            _vim = vim;
        }

        #region IVimWindowManager

        ReadOnlyCollection<IVimWindow> IVimWindowManager.VimWindowList
        {
            get { return new ReadOnlyCollection<IVimWindow>(_map.Values.Cast<IVimWindow>().ToList()); }
        }

        event EventHandler<VimWindowEventArgs> IVimWindowManager.VimWindowCreated
        {
            add { _vimWindowCreated += value; }
            remove { _vimWindowCreated -= value; }
        }

        IVimWindow IVimWindowManager.GetVimWindow(TabItem tabItem)
        {
            return _map[tabItem];
        }

        IVimWindow IVimWindowManager.CreateVimWindow(TabItem tabItem)
        {
            var vimWindow = new VimWindow(_vim, tabItem);
            _map.Add(tabItem, vimWindow);

            var handlers = _vimWindowCreated;
            if (handlers != null)
            {
                handlers(this, new VimWindowEventArgs(vimWindow));
            }

            return vimWindow;
        }

        #endregion 
    }
}
