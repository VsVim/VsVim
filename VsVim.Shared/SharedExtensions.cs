using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Editor;

namespace VsVim
{
    public static class SharedExtensions
    {
        public static Result<IVsCodeWindow> GetCodeWindow(this IVsWindowFrame vsWindowFrame)
        {
            var iid = typeof(IVsCodeWindow).GUID;
            var ptr = IntPtr.Zero;
            try
            {
                ErrorHandler.ThrowOnFailure(vsWindowFrame.QueryViewInterface(ref iid, out ptr));
                return Result.CreateSuccess((IVsCodeWindow)Marshal.GetObjectForIUnknown(ptr));
            }
            catch (Exception e)
            {
                // Venus will throw when querying for the code window
                return Result.CreateError(e);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.Release(ptr);
                }
            }
        }

        /// <summary>
        /// Get the primary view of the code window.  Is actually the one on bottom
        /// </summary>
        public static Result<IVsTextView> GetPrimaryView(this IVsCodeWindow vsCodeWindow)
        {
            IVsTextView vsTextView;
            var hr = vsCodeWindow.GetPrimaryView(out vsTextView);
            if (ErrorHandler.Failed(hr))
            {
                return Result.CreateError(hr);
            }

            return Result.CreateSuccessNonNull(vsTextView);
        }

        /// <summary>
        /// Get the primary view of the code window.  Is actually the one on bottom
        /// </summary>
        public static Result<IWpfTextView> GetPrimaryTextView(this IVsCodeWindow codeWindow, IVsEditorAdaptersFactoryService factoryService)
        {
            var result = GetPrimaryView(codeWindow);
            if (result.IsError)
            {
                return Result.CreateError(result.HResult);
            }

            var textView = factoryService.GetWpfTextView(result.Value);
            return Result.CreateSuccessNonNull(textView);
        }
    }
}
