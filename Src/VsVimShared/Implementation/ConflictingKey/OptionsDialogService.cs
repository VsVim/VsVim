using System.ComponentModel.Composition;
using EditorUtils;
using Vim.UI.Wpf;

namespace VsVim.Implementation.ConflictingKey
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IVimProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal OptionsDialogService(IVimApplicationSettings vimApplicationSettings, IVimProtectedOperations protectedOperations)
        {
            _vimApplicationSettings = vimApplicationSettings;
            _protectedOperations = protectedOperations;
        }

        public bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot)
        {
            return new ConflictingKeyBindingDialog(snapshot, _vimApplicationSettings, _protectedOperations).ShowDialog().Value;
        }
    }
}
