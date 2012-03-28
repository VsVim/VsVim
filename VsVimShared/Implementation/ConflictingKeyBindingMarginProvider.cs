using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace VsVim.Implementation
{
    [Export(typeof(IWpfTextViewMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Top)]
    [ContentType(Vim.Constants.ContentType)]
    [Name(ConflictingKeyBindingMargin.Name)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class ConflictingKeyBindingMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly IVim _vim;
        private readonly IKeyBindingService _keyBindingService;
        private readonly IEditorFormatMapService _formatMapService;
        private readonly ILegacySettings _legacySettings;

        [ImportingConstructor]
        internal ConflictingKeyBindingMarginProvider(IVim vim, IKeyBindingService keyBindingService, IEditorFormatMapService formatMapService, ILegacySettings legacySettings)
        {
            _vim = vim;
            _keyBindingService = keyBindingService;
            _formatMapService = formatMapService;
            _legacySettings = legacySettings;
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var map = _formatMapService.GetEditorFormatMap(wpfTextViewHost.TextView);
            return new ConflictingKeyBindingMargin(_keyBindingService, map, _legacySettings);
        }
    }
}
