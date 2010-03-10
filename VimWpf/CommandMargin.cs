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
        private readonly CommandMarginControl _margin = new CommandMarginControl();
        private readonly IVimBuffer _buffer;

        public CommandMargin(IVimBuffer buffer)
        {
            _buffer = buffer;
            _buffer.SwitchedMode += (sender, args) => _margin.StatusLine= _buffer.Mode.ModeKind.ToString();
            _margin.StatusLine = "Welcome to Vim";
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
            throw new NotImplementedException();
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
