using System;
using System.ComponentModel.Composition;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Implementation.Misc
{
    [Export(typeof(ICommandDispatcher))]
    internal class VisualStudioCommandDispatcher : ICommandDispatcher
    {
        private readonly _DTE _dte;
        private readonly IVsUIShell _uiShell;

        [ImportingConstructor]
        internal VisualStudioCommandDispatcher(SVsServiceProvider serviceProvider)
        {
            _dte = serviceProvider.GetService<SDTE, _DTE>();
            _uiShell = serviceProvider.GetService<SVsUIShell, IVsUIShell>();
        }

        public bool ExecuteCommand(ITextView textView, string command, string args, bool postCommand)
        {
            // Many Visual Studio commands expect the focus to be in the editor
            // when  running.  Switch focus there if an appropriate ITextView
            // is available.
            if (textView is IWpfTextView wpfTextView)
            {
                wpfTextView.VisualElement.Focus();
            }

            // Some commands like 'Edit.GoToDefinition' only work like they do
            // when they are bound a key in Visual Studio like 'F12' when they
            // are posted instead of executed synchronously. See issue #2535.
            if (postCommand)
            {
                var dteCommand = _dte.Commands.Item(command, 0);
                var guid = new Guid(dteCommand.Guid);
                return _uiShell.PostExecCommand(ref guid, (uint)dteCommand.ID, 0, args) == VSConstants.S_OK;
            }
            else
            {
                _dte.ExecuteCommand(command, args);
                return true;
            }
        }
    }
}
