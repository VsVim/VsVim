using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Vim;
using Vim.Extensions;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.Editor;

namespace VsVim.Dev10
{
    internal sealed class SharedService : ISharedService
    {
        private readonly IVsAdapter _vsAdapter;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;

        internal SharedService(IVsAdapter vsAdapter)
        {
            _vsAdapter = vsAdapter;
            _editorAdaptersFactoryService = _vsAdapter.EditorAdapter;
        }

        /// <summary>
        /// Go to the 'count' tab in the given direction.  If the count exceeds the count in
        /// the given direction then it should wrap around to the end of the list of items
        /// </summary>
        internal void GoToNextTab(Vim.Path direction, int count)
        {
            // First get the index of the current tab so we know where we are incrementing
            // from.  Make sure to check that our view is actually a part of the active
            // views
            var children = GetActiveViews();
            var activeView = ViewManager.Instance.ActiveView;
            var index = children.IndexOf(activeView);
            if (index == -1)
            {
                return;
            }

            count = count % children.Count;
            if (direction.IsForward)
            {
                index += count;
                index %= children.Count;
            }
            else
            {
                index -= count;
                if (index < 0)
                {
                    index += children.Count;
                }
            }

            children[index].ShowInFront();
        }

        internal void GoToTab(int index)
        {
            View targetView;
            var children = GetActiveViews();
            if (index < 0)
            {
                targetView = children[children.Count - 1];
            }
            else if (index == 0)
            {
                targetView = children[0];
            }
            else
            {
                index -= 1;
                targetView = index < children.Count ? children[index] : null;
            }

            if (targetView == null)
            {
                return;
            }

            targetView.ShowInFront();
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

        internal FSharpOption<ITextView> GetFocusedTextView()
        {
            var activeView = ViewManager.Instance.ActiveView;
            var result = _vsAdapter.GetWindowFrames();
            if (result.IsError)
            {
                return FSharpOption<ITextView>.None;
            }

            IVsWindowFrame activeWindowFrame = null;
            foreach (var vsWindowFrame in result.Value)
            {
                var frame = vsWindowFrame as WindowFrame;
                if (frame != null && frame.FrameView == activeView)
                {
                    activeWindowFrame = frame;
                    break;
                }
            }

            if (activeWindowFrame == null)
            {
                return FSharpOption<ITextView>.None;
            }

            // TODO: Should try and pick the ITextView which is actually focussed as 
            // there could be several in a split screen
            try
            {
                ITextView textView = activeWindowFrame.GetCodeWindow().Value.GetPrimaryTextView(_editorAdaptersFactoryService).Value;
                return FSharpOption.Create(textView);
            }
            catch
            {
                return FSharpOption<ITextView>.None;
            }
        }


        #region ISharedService

        void ISharedService.GoToNextTab(Path path, int count)
        {
            GoToNextTab(path, count);
        }

        void ISharedService.GoToTab(int index)
        {
            GoToTab(index);
        }

        FSharpOption<ITextView> ISharedService.GetFocusedTextView()
        {
            return GetFocusedTextView();
        }

        #endregion
    }
}
