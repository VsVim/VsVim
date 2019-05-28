using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel.Composition;
using Vim.UI.Wpf;

namespace Vim.VisualStudio
{
    [Export(typeof(IJoinableTaskFactoryProvider))]
    internal class VsVimJoinableTaskFactoryProvider : IJoinableTaskFactoryProvider
    {
        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        internal VsVimJoinableTaskFactoryProvider()
        {
            // See:
            // https://github.com/microsoft/vs-threading/blob/master/doc/library_with_jtf.md
            // https://github.com/microsoft/vs-threading/blob/master/doc/testing_vs.md
            _joinableTaskFactory = ThreadHelper.JoinableTaskFactory;
        }

        public JoinableTaskFactory JoinableTaskFactory => _joinableTaskFactory;

        JoinableTaskFactory IJoinableTaskFactoryProvider.JoinableTaskFactory => JoinableTaskFactory;
    }
}
