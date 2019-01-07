using System;
using System.Windows;
using System.Windows.Controls;

using Microsoft.VisualStudio.Text.Editor;

namespace Vim.UI.Wpf.RelativeLineNumbers
{
    public abstract class VerticalCanvasMargin : IWpfTextViewMargin
    {
        public string MarginName { get; }

        public abstract bool Enabled { get; }

        public double MarginSize => ThrowIfDisposed(Canvas.Width);

        public FrameworkElement VisualElement => ThrowIfDisposed(Canvas);

        protected Canvas Canvas { get; }

        private bool IsDisposed { get; set; }

        protected VerticalCanvasMargin(string marginName)
        {
            MarginName = marginName;

            Canvas = new Canvas()
            {
                ClipToBounds = true,
            };
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return marginName == MarginName
                       ? this
                       : null;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(MarginName);
            }
        }

        private T ThrowIfDisposed<T>(T passThrough)
        {
            ThrowIfDisposed();

            return passThrough;
        }
    }
}
