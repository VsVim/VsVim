using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Collections.Generic;
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

            IEnumerable<VirtualSnapshotPoint> ISharedService.GetCaretPoints(ITextView textView)
            {
                return new[] { textView.Caret.Position.VirtualBufferPosition };
            }

            void ISharedService.SetCaretPoints(ITextView textView, IEnumerable<VirtualSnapshotPoint> caretPoints)
            {
                var caretPoint = caretPoints.First();
                textView.Caret.MoveTo(caretPoint);
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
