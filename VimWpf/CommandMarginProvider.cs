using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace Vim.UI.Wpf
{
    /// <summary>
    /// Factory for creating the margin for Vim
    /// </summary>
    [Export(typeof(IWpfTextViewMarginProvider))]
    [MarginContainer(PredefinedMarginNames.Bottom)]
    [ContentType("code")]
    [Name("Vim Command Margin")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class CommandMarginProvider : IWpfTextViewMarginProvider
    {
        private readonly IVim _vim;

        [ImportingConstructor]
        internal CommandMarginProvider(IVim vim)
        {
            _vim = vim;
        }

        public IWpfTextViewMargin CreateMargin(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin marginContainer)
        {
            var buffer = _vim.GetOrCreateBuffer(wpfTextViewHost.TextView);
            return new CommandMargin(buffer);
        }
    }
}
