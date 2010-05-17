using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim;
using VsVim.UI;
using System.Windows;
using Microsoft.VisualStudio.Text.Classification;
using System.Windows.Media;

namespace VsVim.Implementation
{
    internal sealed class ConflictingKeyBindingMargin : IWpfTextViewMargin
    {
        internal const string Name = "Vim Conflicting KeyBinding Margin";

        private readonly IVimBuffer _buffer;
        private readonly IKeyBindingService _keyBindingService;
        private readonly ConflictingKeyBindingMarginControl _control;
        private bool _enabled = true;

        internal ConflictingKeyBindingMargin(IVimBuffer buffer, IKeyBindingService service, IEditorFormatMap formatMap)
        {
            _buffer = buffer;
            _keyBindingService = service;
            _control = new ConflictingKeyBindingMarginControl();
            _control.Background = GetBackgroundColor(formatMap);

            _control.ConfigureClick += OnConfigureClick;
            _control.IgnoreClick += OnIgnoreClick;
            _keyBindingService.ConflictingKeyBindingStateChanged += OnStateChanged;

            OnStateChanged(this, EventArgs.Empty);
        }

        private static Brush GetBackgroundColor(IEditorFormatMap map)
        {
            var properties = map.GetProperties(EditorFormatDefinitionNames.ConflictingKeyBindingMargin);
            var key = EditorFormatDefinition.BackgroundColorId;
            var color = ConflictingKeyBindingMarginFormatDefinition.DefaultColor;
            if (properties != null && properties.Contains(key))
            {
                color = (Color)properties[key];
            }

            return new SolidColorBrush(color);
        }

        private void Unsubscribe()
        {
            _control.ConfigureClick -= OnConfigureClick;
            _control.IgnoreClick -= OnIgnoreClick;
            _keyBindingService.ConflictingKeyBindingStateChanged -= OnStateChanged;
        }

        private void OnConfigureClick(object sender, EventArgs e)
        {
            _keyBindingService.ResolveAnyConflicts();
        }

        private void OnIgnoreClick(object sender, EventArgs e)
        {
            _keyBindingService.IgnoreAnyConflicts();
            Settings.Settings.Default.IgnoredConflictingKeyBinding = true;
            Settings.Settings.Default.Save();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            switch (_keyBindingService.ConflictingKeyBindingState)
            {
                case ConflictingKeyBindingState.HasNotChecked:
                case ConflictingKeyBindingState.ConflictsIgnoredOrResolved:
                    _control.Visibility = Visibility.Collapsed;
                    _enabled = false;
                    break;
                case ConflictingKeyBindingState.FoundConflicts:
                    _control.Visibility = Visibility.Visible;
                    _enabled = true;
                    break;
                default:
                    throw new Exception("Enum value unknown");
            }
        }

        #region IWpfTextViewMargin

        public System.Windows.FrameworkElement VisualElement
        {
            get { return _control; }
        }

        public bool Enabled
        {
            get { return _enabled; }
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return marginName == Name ? this : null;
        }

        public double MarginSize
        {
            get { return 25d; }
        }

        public void Dispose()
        {
            Unsubscribe();
        }

        #endregion

    }
}
