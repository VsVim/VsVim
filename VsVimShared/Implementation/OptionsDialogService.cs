using System.ComponentModel.Composition;

namespace VsVim.Implementation
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        private readonly ILegacySettings _legacySettings;

        [ImportingConstructor]
        internal OptionsDialogService(ILegacySettings legacySettings)
        {
            _legacySettings = legacySettings;
        }

        public bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot)
        {
            return new UI.ConflictingKeyBindingDialog(snapshot, _legacySettings).ShowDialog().Value;
        }
    }
}
