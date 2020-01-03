using System.ComponentModel.Composition;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
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
            console = IdeServices.ProgressMonitorManager.GetOutputProgressMonitor("Vim Output", Stock.Console, true, false, true);
        }

        void IVimBufferCreationListener.VimBufferCreated(IVimBuffer vimBuffer)
        {
            vimBuffer.StatusMessage += (_, e) => { console.Log.WriteLine(e.Message); };
            vimBuffer.ErrorMessage += (_, e) => { console.ErrorLog.WriteLine(e.Message); };
            vimBuffer.WarningMessage += (_, e) => { console.Log.WriteLine(e.Message); };
        }
    }
}
