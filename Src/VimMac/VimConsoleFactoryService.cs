using System.ComponentModel.Composition;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide.Gui;

namespace Vim.UI.Cocoa
{
    [Export(typeof(IVimBufferCreationListener))]
    internal sealed class VimConsoleFactoryService : IVimBufferCreationListener
    {
        private OutputProgressMonitor console;

        [ImportingConstructor]
        internal VimConsoleFactoryService()
        {
            var monitors = (IdeProgressMonitorManager)Runtime.GetService<ProgressMonitorManager>().Result;
            console = monitors.GetOutputProgressMonitor("Vim Output", Stock.Console, true, false, true);
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            vimBuffer.StatusMessage += (_, e) => { console.Log.WriteLine(e.Message); };
            vimBuffer.ErrorMessage += (_, e) => { console.ErrorLog.WriteLine(e.Message); };
            vimBuffer.WarningMessage += (_, e) => { console.Log.WriteLine(e.Message); };
        }
    }
}
