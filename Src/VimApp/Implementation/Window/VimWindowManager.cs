using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows.Controls;

namespace VimApp.Implementation.Window
{
    [Export(typeof(IVimWindowManager))]
    internal sealed class VimWindowManager : IVimWindowManager
    {
        private readonly Dictionary<TabItem, VimWindow> _map = new Dictionary<TabItem, VimWindow>();

        #region IVimWindowManager

        ReadOnlyCollection<IVimWindow> IVimWindowManager.VimWindowList
        {
            get { return new ReadOnlyCollection<IVimWindow>(_map.Values.Cast<IVimWindow>().ToList()); }
        }

        IVimWindow IVimWindowManager.GetVimWindow(TabItem tabItem)
        {
            return _map[tabItem];
        }

        IVimWindow IVimWindowManager.CreateVimWindow(TabItem tabItem)
        {
            var vimWindow = new VimWindow(tabItem);
            _map.Add(tabItem, vimWindow);
            return vimWindow;
        }

        #endregion 
    }
}
