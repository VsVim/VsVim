
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsVim.Implementation.SharedService
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
