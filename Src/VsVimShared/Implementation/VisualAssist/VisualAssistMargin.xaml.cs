using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Classification;
using System.Diagnostics;

namespace VsVim.Implementation.VisualAssist
{
    internal partial class VisualAssistMargin : UserControl, IWpfTextViewMargin
    {
        private const string MarginName = "Visual Assist Enabled Margin";
        private readonly IVisualAssistUtil _visualAssistUtil;

        internal VisualAssistMargin(IVisualAssistUtil visualAssistUtil, IEditorFormatMap editorFormatMap)
        {
            _visualAssistUtil = visualAssistUtil;
            _visualAssistUtil.IsRegistryFixNeededChanged += IsRegistryFixNeededChanged;
            InitializeComponent();

            Background = editorFormatMap.GetBackgroundBrush(EditorFormatDefinitionNames.Margin, MarginFormatDefinition.DefaultColor);
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            _visualAssistUtil.IsRegistryFixNeeed = false;
        }

        private void OnRequestNavigate(object sender, RoutedEventArgs e)
        {
            var uri = _faqHyperlink.NavigateUri;
            Process.Start(uri.ToString());
            e.Handled = true;
        }

        private void IsRegistryFixNeededChanged(object sender, EventArgs e)
        {
            Unsubscribe();
            Visibility = Visibility.Collapsed;
        }

        private void Unsubscribe()
        {
            _visualAssistUtil.IsRegistryFixNeededChanged -= IsRegistryFixNeededChanged;
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
