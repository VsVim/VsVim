using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim.Implementation.VisualAssist
{
    internal partial class VisualAssistMargin : UserControl, IWpfTextViewMargin
    {
        private const string MarginName = "Visual Assist Enabled Margin";
        private readonly IVisualAssistUtil _visualAssistUtil;
        private bool _isPrimary;

        internal VisualAssistMargin(IVisualAssistUtil visualAssistUtil)
        {
            _visualAssistUtil = visualAssistUtil;
            _visualAssistUtil.RegistryFixCompleted += OnRegistryFixCompleted;
            InitializeComponent();
        }

        private void OnYesClick(object sender, EventArgs e)
        {
            _isPrimary = true;
            _visualAssistUtil.FixRegistry();
        }

        private void OnNoClick(object sender, EventArgs e)
        {
            _visualAssistUtil.IgnoreRegistry();
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            Visibility = Visibility.Collapsed;
        }

        private void OnRegistryFixCompleted(object sender, EventArgs e)
        {
            Unsubscribe();

            if (_isPrimary)
            {
                // This control will appear in every buffer until it is addressed.  If this is the one
                // where the user actually clicked yes on we need to display the restart banner as well
                _fixRegistryPanel.Visibility = Visibility.Collapsed;
                _restartPanel.Visibility = Visibility.Visible;
            }
            else 
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void Unsubscribe()
        {
            _visualAssistUtil.RegistryFixCompleted += OnRegistryFixCompleted;
        }

        #region IWpfTextViewMargin

        FrameworkElement IWpfTextViewMargin.VisualElement
        {
            get { return this; }
        }

        bool ITextViewMargin.Enabled
        {
            get { return Visibility == Visibility.Visible; }
        }

        ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName)
        {
            return marginName == MarginName ? this : null;
        }

        double ITextViewMargin.MarginSize
        {
            get { return 25d; }
        }

        void IDisposable.Dispose()
        {
            Unsubscribe();
        }

        #endregion
    }
}
