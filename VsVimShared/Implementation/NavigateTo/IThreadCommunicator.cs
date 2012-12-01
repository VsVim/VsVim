
namespace VsVim.Implementation.NavigateTo
{
    /// <summary>
    /// This interface is usable from multiple threads
    /// </summary>
    internal interface IThreadCommunicator
    {
        void StartSearch();
        void StopSearch();
    }
}
