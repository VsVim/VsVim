using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace VsVim.Implementation.ToastNotification
{
    internal sealed class ToastNotificationService : IToastNotificationService, IWpfTextViewMargin
    {
        internal const string MarginName = "Toast Notification Service";

        private readonly IWpfTextView _wpfTextView;
        private readonly ToastControl _toastControl;
        private readonly IEditorFormatMap _editorFormatMap;
        
        internal ToastControl ToastControl
        {
            get { return _toastControl; }
        }

        internal ToastNotificationService(IWpfTextView wpfTextView, IEditorFormatMap editorFormatMap)
        {
            _wpfTextView = wpfTextView;
            _editorFormatMap = editorFormatMap;
            _toastControl = new ToastControl();
            _toastControl.Visibility = Visibility.Collapsed;
            _toastControl.ToastNotificationCollection.CollectionChanged += OnToastControlItemsChanged;

            _editorFormatMap.FormatMappingChanged += OnEditorFormatMappingChanged;
            UpdateTheme();
        }

        private void UpdateTheme()
        {
            _toastControl.Background = _editorFormatMap.GetBackgroundBrush(EditorFormatDefinitionNames.Margin, MarginFormatDefinition.DefaultColor);
        }

        private void OnToastControlItemsChanged(object sender, EventArgs e)
        {
            _toastControl.Visibility = _toastControl.ToastNotificationCollection.Count > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void OnEditorFormatMappingChanged(object sender, EventArgs e)
        {
            UpdateTheme();
        }

        #region IWpfTextViewMargin

        bool ITextViewMargin.Enabled
        {
            get { return _toastControl.ToastNotificationCollection.Count > 0; }
        }

        double ITextViewMargin.MarginSize
        {
            get { return 25; }
        }

        FrameworkElement IWpfTextViewMargin.VisualElement
        {
            get { return _toastControl; }
        }

        #endregion

        #region IToastNotificationService

        IWpfTextView IToastNotificationService.TextView
        {
            get { return _wpfTextView; }
        }

        void IToastNotificationService.Display(FrameworkElement frameworkElement)
        {
            _toastControl.ToastNotificationCollection.Add(frameworkElement);

            frameworkElement.IsVisibleChanged += delegate
            {
                if (frameworkElement.Visibility == Visibility.Collapsed)
                {
                    _toastControl.ToastNotificationCollection.Remove(frameworkElement);
                }
            };
        }

        #endregion

        #region ITextView

        ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName)
        {
            return marginName == MarginName ? this : null;
        }

        #endregion 

        #region IDisposable

        void IDisposable.Dispose()
        {
            _editorFormatMap.FormatMappingChanged -= OnEditorFormatMappingChanged;
        }

        #endregion
    }
}
