﻿using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Text.Editor;
using Vim;

namespace VsVim.Implementation.NavigateTo
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
        private readonly ITextManager _textManager;
        private bool _inSearch;

        [ImportingConstructor]
        internal NavigateToItemProviderFactory(ITextManager textManager)
        {
            _textManager = textManager;
            _synchronizationContext = WindowsFormsSynchronizationContext.Current;
        }

        private void OnSearchStarted()
        {
            _inSearch = true;
        }

        private void OnSearchStopped()
        {
            if (_inSearch)
            {
                // Once the search is stopped clear out all of the selections in active buffers.  Leaving the 
                // selection puts us into Visual Mode.  Don't force any document loads here.  If the document 
                // isn't loaded then it can't have a selection which will interfere with this
                _inSearch = false;
                _textManager.GetDocumentTextViews(DocumentLoad.RespectLazy)
                    .Where(x => !x.Selection.IsEmpty)
                    .ForEach(x => x.Selection.Clear());
            }
        }

        private void CallOnMainThread(Action action)
        {
            Action wrappedAction = () =>
            {
                try
                {
                    action();
                }
                catch
                {
                    // Don't let the exception propagate to the message loop and take down VS
                }
            };

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
        void IThreadCommunicator.StartSearch()
        {
            CallOnMainThread(OnSearchStarted);
        }

        /// <summary>
        /// WARNING!!! This method is executed from a background thread
        /// </summary>
        void IThreadCommunicator.StopSearch()
        {
            CallOnMainThread(OnSearchStopped);
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
