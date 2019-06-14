using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Vim.UnitTest
{
    public sealed class WpfTextViewDisplay : IDisposable
    {
        public IWpfTextViewHost TextViewHost { get; }
        public Window Window { get; }
        public IWpfTextView TextView => TextViewHost.TextView;

        public WpfTextViewDisplay(IWpfTextViewHost textViewHost, int maxHeight = 240, int maxWidth = 240)
        {
            TextViewHost = textViewHost;
            Window = new Window()
            {
                MaxHeight = maxHeight,
                MaxWidth = maxWidth,
                Content = textViewHost.HostControl
            };
        }

        public void Show()
        {
            Window.Show();
        }

        public void Dispose()
        {
            Window.Hide();
        }

        #region IDisposable

        void IDisposable.Dispose() => Dispose();

        #endregion
    }
}
