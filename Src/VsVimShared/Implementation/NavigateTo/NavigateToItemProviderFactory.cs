﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using System.Windows.Input;
using System.Collections.Generic;
using Vim.VisualStudio.Implementation.Misc;

// Have to use SynchronizationContext due to the implementation details of NavigateTo
#pragma warning disable VSTHRD001

namespace Vim.VisualStudio.Implementation.NavigateTo
{
    /// <summary>
    /// Certain implementations of the NagivateTo / QuickSearch feature will select the
    /// target of the navigation.  This causes VsVim to incorrectly enter Visual Mode and
    /// is distracting to developers.  This set of types is intended to prevent the entering
    /// of visual mode in this scenario
    /// </summary>
    [Export(typeof(INavigateToItemProviderFactory))]
    [Export(typeof(IVisualModeSelectionOverride))]
    internal sealed class NavigateToItemProviderFactory : INavigateToItemProviderFactory, IThreadCommunicator, IVisualModeSelectionOverride
    {
        /// <summary>
        /// Ideally we would use IProtectedOperations here.  However NavigateTo Window is a Windows Form
        /// window and it suppresses the pumping of the WPF message loop which IProtectedOperations 
        /// depends on.  Using the WindowsFormsSynchronizationContext out of necessity here
        /// </summary>
        private readonly SynchronizationContext _synchronizationContext;
        private readonly IVim _vim;
        private readonly ITextManager _textManager;
        private readonly UnwantedSelectionHandler _unwantedSelectionHandler;
        private bool _inSearch;

        [ImportingConstructor]
        internal NavigateToItemProviderFactory(IVim vim, ITextManager textManager)
        {
            _vim = vim;
            _textManager = textManager;
            _synchronizationContext = WindowsFormsSynchronizationContext.Current;
            _unwantedSelectionHandler = new UnwantedSelectionHandler(_vim);
        }

        private void OnSearchStarted(string searchText)
        {
            _unwantedSelectionHandler.PreAction();
            _inSearch = true;
            VimTrace.TraceInfo("NavigateTo Start: {0}", searchText);
        }

        private void OnSearchStopped(string searchText)
        {
            VimTrace.TraceInfo("NavigateTo Stop: {0}", searchText);
            if (_inSearch)
            {
                _inSearch = false;
                _unwantedSelectionHandler.PostAction();
            }
        }

        private void Dispose()
        {
            VimTrace.TraceInfo("NavigateTo Disposed");

            // In some configurations the C++ editor will not set focus to the ITextView which is displayed
            // as a result of completing a NavigateTo operation.  Instead focus will be on the navigation 
            // bar.  This is not a function of VsVim but does mess up the general keyboard usage and 
            // hence we force the focus to be correct
            //
            // Note: The exact scenarios under which this happens is not well understood.  It does repro under
            // a clean machine and Windows 8.1 but doesn't always repro under other configurations.  Either way
            // need to fix
            if (_textManager.ActiveTextViewOptional is IWpfTextView wpfTextView && !wpfTextView.HasAggregateFocus && wpfTextView.TextSnapshot.ContentType.IsCPlusPlus())
            {
                VimTrace.TraceInfo("NavigateTo adjust C++ focus");
                Keyboard.Focus(wpfTextView.VisualElement);
            }
        }

        private void CallOnMainThread(Action action)
        {
            void wrappedAction()
            {
                try
                {
                    action();
                }
                catch
                {
                    // Don't let the exception propagate to the message loop and take down VS
                }
            }

            try
            {
                _synchronizationContext.Post(_ => wrappedAction(), null);
            }
            catch
            {
                // The set of _inSearch is guaranteed to be atomic because it's a bool.  In the
                // case an exception occurs and we can't communicate with the UI thread we should
                // just act as if no search is going on.  Worst case Visual mode is incorrectly
                // entered in a few projects
                _inSearch = false;
            }
        }

        #region INavigateToItemProviderFactory

        /// <summary>
        /// WARNING!!! This method is executed from a background thread
        /// </summary>
        bool INavigateToItemProviderFactory.TryCreateNavigateToItemProvider(IServiceProvider serviceProvider, out INavigateToItemProvider navigateToItemProvider)
        {
            navigateToItemProvider = new NavigateToItemProvider(this);
            return true;
        }

        #endregion

        #region IThreadCommunicator

        /// <summary>
        /// WARNING!!! This method is executed from a background thread
        /// </summary>
        void IThreadCommunicator.StartSearch(string searchText)
        {
            CallOnMainThread(() => OnSearchStarted(searchText));
        }

        /// <summary>
        /// WARNING!!! This method is executed from a background thread
        /// </summary>
        void IThreadCommunicator.StopSearch(string searchText)
        {
            CallOnMainThread(() => OnSearchStopped(searchText));
        }

        /// <summary>
        /// WARNING!!! This method is executed from a background thread
        /// </summary>
        void IThreadCommunicator.Dispose()
        {
            CallOnMainThread(Dispose);
        }

        #endregion

        #region IVisualModeSelectionOverride

        bool IVisualModeSelectionOverride.IsInsertModePreferred(ITextView textView)
        {
            return _inSearch;
        }

        #endregion
    }
}
