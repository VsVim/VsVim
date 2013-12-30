using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
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
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly IVim _vim;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactories;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly SVsServiceProvider _serviceProvider;

        internal const string CategoryFontsAndColors = "FontsAndColors";
        internal const string PageTextEditor = "TextEditor";

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim, 
            IEditorFormatMapService editorFormatMapService, 
            SVsServiceProvider serviceProvider,
            [ImportMany] IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactories)
        {
            _vim = vim;
            _editorFormatMapService = editorFormatMapService;
            _serviceProvider = serviceProvider;
            _optionsProviderFactories = optionsProviderFactories.ToList().AsReadOnly();
        }

        #region IWpfTextViewMarginProvider

        IWpfTextViewMargin IWpfTextViewMarginProvider.CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            IVimBuffer vimBuffer;
            if (!_vim.TryGetOrCreateVimBufferForHost(wpfTextViewHost.TextView, out vimBuffer))
            {
                return null;
            }

            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(wpfTextViewHost.TextView);
            var dte = (_DTE)_serviceProvider.GetService(typeof(_DTE));
            var fontProperties = dte.Properties[CategoryFontsAndColors, PageTextEditor];

			return new CommandMargin(wpfTextViewHost.TextView.VisualElement, vimBuffer, editorFormatMap, fontProperties, _optionsProviderFactories);
        }

        #endregion
    }
}
