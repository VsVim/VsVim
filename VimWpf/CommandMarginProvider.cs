using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Collections.ObjectModel;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Factory for creating the margin for Vim
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("code")]
    [Name(CommandMargin.Name)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly IVim _vim;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactories;

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim, 
            [ImportMany] IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactories)
        {
            _vim = vim;
            _optionsProviderFactories = optionsProviderFactories.ToList().AsReadOnly();
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var buffer = _vim.GetOrCreateBuffer(wpfTextViewHost.TextView);
            return new CommandMargin(buffer,_optionsProviderFactories);
        }
    }
}
