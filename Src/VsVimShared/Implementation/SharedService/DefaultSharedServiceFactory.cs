using Microsoft.FSharp.Core;
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

            void ISharedService.RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
            {
                vimBuffer.VimBufferData.StatusUtil.OnError("csx not supported");
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
