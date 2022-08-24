using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using System.ComponentModel.Composition;
using Vim.UI.Wpf;

namespace VimApp
{
    [Export(typeof(IJoinableTaskFactoryProvider))]
    internal sealed class VimAppJoinableTaskFactoryProvider : IJoinableTaskFactoryProvider
    {
        private static readonly object s_lock = new object();
        private static JoinableTaskContext s_joinableTaskContext;

        private readonly JoinableTaskFactory _joinableTaskFactory;

        [ImportingConstructor]
        internal VimAppJoinableTaskFactoryProvider()
        {
            // See:
            // https://github.com/microsoft/vs-threading/blob/master/doc/library_with_jtf.md
            // https://github.com/microsoft/vs-threading/blob/master/doc/testing_vs.md
            lock (s_lock)
            {
                if (s_joinableTaskContext == null)
                {
                    s_joinableTaskContext = ThreadHelper.JoinableTaskContext;
                }
            }
            _joinableTaskFactory = s_joinableTaskContext.Factory;
        }

        public JoinableTaskFactory JoinableTaskFactory => _joinableTaskFactory;

        JoinableTaskFactory IJoinableTaskFactoryProvider.JoinableTaskFactory => JoinableTaskFactory;
    }
}
