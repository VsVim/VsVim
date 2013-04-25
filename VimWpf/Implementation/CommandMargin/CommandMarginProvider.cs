using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using Vim.Extensions;

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


        #region IWpfTextViewMarginProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var option = _vim.GetOrCreateVimBufferForHost(wpfTextViewHost.TextView);
            if (option.IsNone())
            {
                return null;
            }

            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextViewHost.TextView);
			return new CommandMargin(wpfTextViewHost.TextView.VisualElement, option.Value, editorFormatMap, _optionsProviderFactories);
        }

        #endregion
    }
}
