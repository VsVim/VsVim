
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.FSharp.Core;
namespace VsVim.Implementation
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

            FSharpOption<ITextView> ISharedService.GetFocusedTextView()
            {
                return FSharpOption<ITextView>.None;
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
