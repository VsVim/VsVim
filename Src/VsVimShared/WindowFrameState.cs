using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Interpreter;

namespace Vim.VisualStudio
{
    /// <summary>
    /// State of the active tab group in Visual Studio
    /// </summary>
    public readonly struct WindowFrameState
    {
        public static WindowFrameState Default
        {
            get { return new WindowFrameState(activeWindowFrameIndex: 0, windowFrameCount: 1); }
        }

        public readonly int ActiveWindowFrameIndex;
        public readonly int WindowFrameCount;

        public WindowFrameState(int activeWindowFrameIndex, int windowFrameCount)
        {
            ActiveWindowFrameIndex = activeWindowFrameIndex;
            WindowFrameCount = windowFrameCount;
        }
    }
}
