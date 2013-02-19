using System.ComponentModel.Composition;

namespace VsVim.Implementation.ConflictingKey
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        private readonly IVimApplicationSettings _vimApplicationSettings;

        [ImportingConstructor]
        internal OptionsDialogService(IVimApplicationSettings vimApplicationSettings)
        {
            _vimApplicationSettings = vimApplicationSettings;
        }

        public bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot)
        {
            return new ConflictingKeyBindingDialog(snapshot, _vimApplicationSettings).ShowDialog().Value;
        }
    }
}
