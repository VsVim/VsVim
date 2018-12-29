using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Interpreter;

namespace Vim.VisualStudio.Implementation.SharedService
{
    internal sealed class DefaultSharedServiceFactory : ISharedServiceVersionFactory
    {
        private sealed class DefaultSharedService : ISharedService
        {
            WindowFrameState ISharedService.GetWindowFrameState()
            {
                return WindowFrameState.Default;
            }

            void ISharedService.GoToTab(int index)
            {
            }

            bool ISharedService.IsActiveWindowFrame(IVsWindowFrame vsWindowFrame)
            {
                return false;
            }

            bool ISharedService.IsLazyLoaded(uint documentCookie)
            {
                return false;
            }

            bool ISharedService.ClosePeekView(ITextView peekView)
            {
                return false;
            }

            void ISharedService.RunCSharpScript(IVim vim, CallInfo callInfo, bool createEachTime)
            {
                vim.ActiveStatusUtil.OnError("csx not supported");
            }
        }

        VisualStudioVersion ISharedServiceVersionFactory.Version
        {
            get { return VisualStudioVersion.Unknown; }
        }

        ISharedService ISharedServiceVersionFactory.Create()
        {
            return new DefaultSharedService();
        }
    }
}
