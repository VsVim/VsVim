using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using System.Windows;

namespace Vim.UI.Wpf
{
    internal sealed class CommandMargin : IWpfTextViewMargin
    {
        internal const string Name = "Vim Command Margin";

        private readonly CommandMarginControl _margin = new CommandMarginControl();
        private readonly CommandMarginController _controller;
        private readonly IVimBuffer _buffer;

        public CommandMargin(IVimBuffer buffer)
        {
            _buffer = buffer;
            _margin.StatusLine = "Welcome to Vim";
            _controller = new CommandMarginController(buffer, _margin);
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
