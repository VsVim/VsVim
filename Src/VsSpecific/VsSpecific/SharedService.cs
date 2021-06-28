using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Platform.WindowManagement;
using Microsoft.VisualStudio.PlatformUI.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using System.ComponentModel.Composition.Hosting;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Interpreter;
using Vim.VisualStudio.Specific.Implementation.WordCompletion;
using Microsoft.FSharp.Core;
using System.ComponentModel.Composition.Primitives;
using System;
using Vim.Extensions;

namespace Vim.VisualStudio.Specific
{
    internal sealed partial class SharedService : ISharedService
    {
        internal SVsServiceProvider VsServiceProvider { get; }
        internal IComponentModel ComponentModel { get; }
        internal ExportProvider ExportProvider { get; }

        internal SharedService(SVsServiceProvider vsServiceProvider)
        {
            VsServiceProvider = vsServiceProvider;
            ComponentModel = (IComponentModel)vsServiceProvider.GetService(typeof(SComponentModel));
            ExportProvider = ComponentModel.DefaultExportProvider;
        }

        internal void GoToTab(int index)
        {
            GetActiveViews()[index].ShowInFront();
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

        void ISharedService.RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            RunCSharpScript(vimBuffer, callInfo, createEachTime);
        }

        #endregion
    }
}
