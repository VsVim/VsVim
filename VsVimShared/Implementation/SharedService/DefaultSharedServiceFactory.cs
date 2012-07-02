
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.FSharp.Core;

namespace VsVim.Implementation.SharedService
{
    internal sealed class DefaultSharedServiceFactory : ISharedServiceVersionFactory
    {
        private sealed class DefaultSharedService : ISharedService
        {
            void ISharedService.GoToNextTab(Vim.Path path, int count)
            {

            }

            void ISharedService.GoToTab(int index)
            {

            }

            bool ISharedService.TryGetFocusedTextView(out ITextView textView)
            {
                textView = null;
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
