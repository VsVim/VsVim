using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    internal sealed class CommandMargin : IWpfTextViewMargin
    {
        internal const string Name = "Vim Command Margin";

        private readonly CommandMarginControl _margin = new CommandMarginControl();
        private readonly CommandMarginController _controller;

        public CommandMargin(FrameworkElement parentVisualElement, IVimBuffer buffer, IEditorFormatMap editorFormatMap, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactories)
        {
            _margin.StatusLine = "Welcome to Vim";
            _controller = new CommandMarginController(buffer, parentVisualElement, _margin, editorFormatMap, optionsProviderFactories);
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
            return marginName == Name ? this : null;
        }

        public double MarginSize
        {
            get { return 25; }
        }

        public void Dispose()
        {
            _controller.Disconnect();
        }
    }
}
