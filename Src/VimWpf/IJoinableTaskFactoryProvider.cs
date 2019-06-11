using Microsoft.VisualStudio.Threading;

namespace Vim.UI.Wpf
{
    public interface IJoinableTaskFactoryProvider
    {
        JoinableTaskFactory JoinableTaskFactory { get; }
    }
}
