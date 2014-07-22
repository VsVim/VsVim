
namespace Vim.VisualStudio.Implementation.NavigateTo
{
    /// <summary>
    /// This interface is usable from multiple threads
    /// </summary>
    internal interface IThreadCommunicator
    {
        void StartSearch(string text);
        void StopSearch(string text);
        void Dispose();
    }
}
