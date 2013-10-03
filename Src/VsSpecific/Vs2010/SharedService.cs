using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsVim.Vs2010
{
    internal sealed class SharedService : ISharedService
    {
        internal SharedService()
        {

        }

        internal void GoToTab(int index)
        {
            GetActiveViews()[index].ShowInFront();
        }

        internal WindowFrameState GetWindowFrameState()
        {
            var activeView = ViewManager.Instance.ActiveView;
            if (activeView == null)
            {
                return WindowFrameState.Default;
            }

            var list = GetActiveViews();
            var index = list.IndexOf(activeView);
            if (index < 0)
            {
                return WindowFrameState.Default;
            }

            return new WindowFrameState(index, list.Count);
        }

        /// <summary>
        /// Get the list of View's in the current ViewManager DocumentGroup
        /// </summary>
        private static List<View> GetActiveViews()
        {
            var activeView = ViewManager.Instance.ActiveView;
            if (activeView == null)
            {
                return new List<View>();
            }

            var group = activeView.Parent as DocumentGroup;
            if (group == null)
            {
                return new List<View>();
            }

            return group.VisibleChildren.OfType<View>().ToList();
        }

        internal bool IsActiveWindowFrame(IVsWindowFrame vsWindowFrame)
        {
            var frame = vsWindowFrame as WindowFrame;
            return frame != null && frame.FrameView == ViewManager.Instance.ActiveView;
        }

        #region ISharedService

        WindowFrameState ISharedService.GetWindowFrameState()
        {
            return GetWindowFrameState();
        }

        void ISharedService.GoToTab(int index)
        {
            GoToTab(index);
        }

        bool ISharedService.IsActiveWindowFrame(IVsWindowFrame vsWindowFrame)
        {
            return IsActiveWindowFrame(vsWindowFrame);
        }

        bool ISharedService.IsLazyLoaded(uint documentCookie)
        {
            return false;
        }

        #endregion
    }
}
