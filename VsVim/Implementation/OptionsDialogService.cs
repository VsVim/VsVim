using System.ComponentModel.Composition;

namespace VsVim.Implementation
{
    [Export(typeof(IOptionsDialogService))]
    internal sealed class OptionsDialogService : IOptionsDialogService
    {
        public bool ShowConflictingKeyBindingsDialog(CommandKeyBindingSnapshot snapshot)
        {
            return new UI.ConflictingKeyBindingDialog(snapshot).ShowDialog().Value;
        }
    }
}
