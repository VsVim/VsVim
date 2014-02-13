using System;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;

namespace VsVim.Implementation.NavigateTo
{
    /// <summary>
    /// WARNING!!!  This class executes entirely on a background thread.  
    /// </summary>
    internal sealed class NavigateToItemProvider : INavigateToItemProvider
    {
        private readonly IThreadCommunicator _threadCommunicator;
        private string _searchText = "";

        internal NavigateToItemProvider(IThreadCommunicator threadCommunicator)
        {
            _threadCommunicator = threadCommunicator;
        }

        #region INavigateToItemProvider

        void INavigateToItemProvider.StartSearch(INavigateToCallback callback, string searchValue)
        {
            _searchText = searchValue;
            _threadCommunicator.StartSearch(searchValue);
            callback.Done();
        }

        void INavigateToItemProvider.StopSearch()
        {
            _threadCommunicator.StopSearch(_searchText);
            _searchText = "";
        }

        void IDisposable.Dispose()
        {
            _threadCommunicator.Dispose();
        }

        #endregion
    }
}
