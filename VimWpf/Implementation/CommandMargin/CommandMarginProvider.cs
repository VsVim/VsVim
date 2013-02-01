using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    /// <summary>
    /// Factory for creating the margin for Vim
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType(Vim.Constants.ContentType)]
    [Name(CommandMargin.Name)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly IVim _vim;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactories;
        private readonly IEditorFormatMapService _editorFormatMapService;

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim, 
            IEditorFormatMapService editorFormatMapService, 
            [ImportMany] IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactories)
        {
            _vim = vim;
            _editorFormatMapService = editorFormatMapService;
            _optionsProviderFactories = optionsProviderFactories.ToList().AsReadOnly();
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var buffer = _vim.GetOrCreateVimBuffer(wpfTextViewHost.TextView);
            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextViewHost.TextView);
			return new CommandMargin(wpfTextViewHost.TextView.VisualElement, buffer, editorFormatMap, _optionsProviderFactories);
        }
    }
}
