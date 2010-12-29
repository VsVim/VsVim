using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf
{
    internal sealed class CommandMargin : IWpfTextViewMargin
    {
        internal const string Name = "Vim Command Margin";

        private readonly CommandMarginControl _margin = new CommandMarginControl();
        private readonly CommandMarginController _controller;
        private readonly IVimBuffer _buffer;

        public CommandMargin(IVimBuffer buffer, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactories)
        {
            _buffer = buffer;
            _margin.StatusLine = "Welcome to Vim";
            _controller = new CommandMarginController(buffer, _margin, optionsProviderFactories);
        }

        public FrameworkElement VisualElement
        {
            get { return _margin; }
        }

        public bool Enabled
        {
            get { return true; }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            if (marginName == Name)
            {
                return this;
            }
            return null;
        }

        public double MarginSize
        {
            get { return 25; }
        }

        public void Dispose()
        {

        }
    }
}
