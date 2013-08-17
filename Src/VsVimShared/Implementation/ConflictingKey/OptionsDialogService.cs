using System.ComponentModel.Composition;
using EditorUtils;

namespace VsVim.Implementation.ConflictingKey
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly IProtectedOperations _protectedOperations;

        [ImportingConstructor]
        internal OptionsDialogService(IVimApplicationSettings vimApplicationSettings, [EditorUtilsImport] IProtectedOperations protectedOperations)
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
