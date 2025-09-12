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
    /// <summary>
    /// Manages Vim windows within the application.
    /// </summary>
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

        /// <summary>
        /// Gets the list of Vim windows.
        /// </summary>
        ReadOnlyCollection<IVimWindow> IVimWindowManager.VimWindowList
        {
            get { return new ReadOnlyCollection<IVimWindow>(_map.Values.Cast<IVimWindow>().ToList()); }
        }

        /// <summary>
        /// Occurs when a Vim window is created.
        /// </summary>
        event EventHandler<VimWindowEventArgs> IVimWindowManager.VimWindowCreated
        {
            add { _vimWindowCreated += value; }
            remove { _vimWindowCreated -= value; }
        }

        /// <summary>
        /// Gets the Vim window associated with the specified TabItem.
        /// </summary>
        IVimWindow IVimWindowManager.GetVimWindow(TabItem tabItem)
        {
            if (_map.TryGetValue(tabItem, out var vimWindow))
            {
                return vimWindow;
            }
            throw new KeyNotFoundException("The specified TabItem does not have an associated VimWindow.");
        }

        /// <summary>
        /// Creates a new Vim window for the specified TabItem.
        /// </summary>
        IVimWindow IVimWindowManager.CreateVimWindow(TabItem tabItem)
        {
            var vimWindow = new VimWindow(_vim, tabItem);
            _map.Add(tabItem, vimWindow);

            _vimWindowCreated?.Invoke(this, new VimWindowEventArgs(vimWindow));

            return vimWindow;
        }

        #endregion 
    }
}
