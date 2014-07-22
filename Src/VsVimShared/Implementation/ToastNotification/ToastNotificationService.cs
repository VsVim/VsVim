using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Vim.VisualStudio.Implementation.ToastNotification
{
    internal sealed class ToastNotificationService : IToastNotificationService, IWpfTextViewMargin
    {
        internal const string MarginName = "Toast Notification Service";

        private struct ToastData
        {
            internal readonly object Key;
            internal readonly FrameworkElement ToastNotification;
            internal readonly Action OnRemove;

            internal ToastData(object key, FrameworkElement toastNotification, Action onRemove)
            {
                Key = key;
                ToastNotification = toastNotification;
                OnRemove = onRemove ?? (() => { });
            }
        }

        private readonly IWpfTextView _wpfTextView;
        private readonly ToastControl _toastControl;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly Dictionary<object, ToastData> _toastDataMap = new Dictionary<object, ToastData>();
        
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
            _wpfTextView.Closed += OnTextViewClosed;
            UpdateTheme();
        }

        private void Unsubscribe()
        {
            _toastControl.ToastNotificationCollection.Clear();
            _toastControl.ToastNotificationCollection.CollectionChanged -= OnToastControlItemsChanged;
            _editorFormatMap.FormatMappingChanged -= OnEditorFormatMappingChanged;
            _wpfTextView.Closed -= OnTextViewClosed;
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

            var removedList = _toastDataMap
                .Where(pair => !_toastControl.ToastNotificationCollection.Contains(pair.Value.ToastNotification))
                .ToList();

            foreach (var pair in removedList)
            {
                pair.Value.OnRemove();
            }

            foreach (var pair in removedList)
            {
                _toastDataMap.Remove(pair.Key);
            }
        }

        private void OnTextViewClosed(object sender, EventArgs e)
        {
            Unsubscribe();
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

        void IToastNotificationService.Display(object key, FrameworkElement toastNotification, Action onRemoveCallback)
        {
            _toastControl.ToastNotificationCollection.Add(toastNotification);
            _toastDataMap.Add(key, new ToastData(key, toastNotification, onRemoveCallback));
        }

        bool IToastNotificationService.Remove(object key)
        {
            ToastData toastData;
            if (!_toastDataMap.TryGetValue(key, out toastData))
            {
                return false;
            }

            return _toastControl.ToastNotificationCollection.Remove(toastData.ToastNotification);
        }

        #endregion

        #region ITextViewMargin

        ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName)
        {
            return marginName == MarginName ? this : null;
        }

        #endregion 

        #region IDisposable

        void IDisposable.Dispose()
        {
            Unsubscribe();
        }

        #endregion
    }
}
