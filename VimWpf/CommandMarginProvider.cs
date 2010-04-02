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
        private readonly ReadOnlyCollection<Lazy<IOptionsPageFactory>> _optionsPageFactories;

        [ImportingConstructor]
        internal CommandMarginProvider(
            IVim vim, 
            [ImportMany] IEnumerable<Lazy<IOptionsPageFactory>> optionsPageFactories)
        {
            _vim = vim;
            _optionsPageFactories = optionsPageFactories.ToList().AsReadOnly();
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var buffer = _vim.GetOrCreateBuffer(wpfTextViewHost.TextView);
            return new CommandMargin(buffer,_optionsPageFactories);
        }
    }
}
