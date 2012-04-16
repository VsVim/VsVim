using System;
using System.Diagnostics;
using Vim;
using Vim.Extensions;

namespace VsVim.Implementation.VisualAssist
{
    internal sealed class VisualAssistKeyProcessor
    {
        private readonly IVim _vim;
        private readonly ISharedService _sharedService;

        /// <summary>
        /// Need to save a copy of this to keep it from being garbage collected.  If it's unreference it
        /// will be collected and the native thunk will be invalidated
        /// </summary>
        private readonly NativeMethods.WindowProcCallback _windowProcCallback;
        private IntPtr _previousWndProc;

        private VisualAssistKeyProcessor(
            IVim vim,
            ISharedService sharedService)
        {
            _vim = vim;
            _sharedService = sharedService;
            _windowProcCallback = new NativeMethods.WindowProcCallback(WindowProc);
        }

        /// <summary>
        /// The escape key was pressed.  If we are currently in insert mode we need to leave it because it 
        /// means that Visual Assist swallowed the key stroke
        /// </summary>
        private void HandleEscape()
        {
            var textViewOption = _sharedService.GetFocusedTextView();
            if (textViewOption.IsNone())
            {
                return;
            }

            var vimBufferOption = _vim.GetVimBuffer(textViewOption.Value);
            if (vimBufferOption.IsNone())
            {
                return;
            }

            var vimBuffer = vimBufferOption.Value;
            if (vimBuffer.ModeKind == ModeKind.Insert)
            {
                vimBuffer.Process(KeyInputUtil.EscapeKey);
            }
        }

        /// <summary>
        /// Look for the Escape key going up.  Visual Assist swallows the downward stroke so we 
        /// look for the up version
        /// </summary>
        private int WindowProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == NativeMethods.WM_KEYUP && wParam != IntPtr.Zero)
            {
                var wParamValue = (uint)(int)(wParam);
                if (wParamValue == NativeMethods.VK_ESCAPE)
                {
                    HandleEscape();
                }
            }

            return NativeMethods.CallWindowProc(_previousWndProc, hwnd, msg, wParam, lParam);
        }

        internal static bool TryCreate(IVim vim, ISharedService sharedService, out VisualAssistKeyProcessor visualAssistKeyProcessor)
        {
            visualAssistKeyProcessor = null;

            var process = Process.GetCurrentProcess();
            var mainWindowHandle = process.MainWindowHandle;
            if (mainWindowHandle == IntPtr.Zero)
            {
                return false;
            }

            var keyProcessor = new VisualAssistKeyProcessor(vim, sharedService);
            keyProcessor._previousWndProc = (IntPtr)NativeMethods.SetWindowLong(process.MainWindowHandle, NativeMethods.GWLP_WNDPROC, keyProcessor._windowProcCallback);
            if (keyProcessor._previousWndProc == IntPtr.Zero)
            {
                return false;
            }

            visualAssistKeyProcessor = keyProcessor;
            return true;
        }
    }
}
