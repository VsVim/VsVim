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

        internal NavigateToItemProvider(IThreadCommunicator threadCommunicator)
        {
            _threadCommunicator = threadCommunicator;
        }

        #region INavigateToItemProvider

        void INavigateToItemProvider.StartSearch(INavigateToCallback callback, string searchValue)
        {
            _threadCommunicator.StartSearch();
        }

        void INavigateToItemProvider.StopSearch()
        {
            _threadCommunicator.StopSearch();
        }

        void IDisposable.Dispose()
        {
            
        }

        #endregion
    }
}
