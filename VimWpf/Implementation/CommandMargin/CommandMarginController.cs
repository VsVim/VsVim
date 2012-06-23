using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Microsoft.VisualStudio.Text.Classification;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    internal sealed class CommandMarginController
    {
        private readonly IVimBuffer _buffer;
        private readonly CommandMarginControl _margin;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactory;
        private bool _inKeyInputEvent;
        private string _message;
        private IMode _modeSwitch;

        internal CommandMarginController(IVimBuffer buffer, CommandMarginControl control, IEditorFormatMap editorFormatMap, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactory)
        {
            _buffer = buffer;
            _margin = control;
            _editorFormatMap = editorFormatMap;
            _optionsProviderFactory = optionsProviderFactory.ToList().AsReadOnly();

            _buffer.SwitchedMode += OnSwitchMode;
            _buffer.KeyInputStart += OnKeyInputStart;
            _buffer.KeyInputEnd += OnKeyInputEnd;
            _buffer.StatusMessage += OnStatusMessage;
            _buffer.ErrorMessage += OnErrorMessage;
            _buffer.WarningMessage += OnWarningMessage;
            _buffer.Vim.MacroRecorder.RecordingStarted += OnRecordingStarted;
            _buffer.Vim.MacroRecorder.RecordingStopped += OnRecordingStopped;
            _margin.OptionsClicked += OnOptionsClicked;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
            UpdateForRecordingChanged();
            UpdateTextColor();
        }

        internal void Disconnect()
        {
            _buffer.Vim.MacroRecorder.RecordingStarted -= OnRecordingStarted;
            _buffer.Vim.MacroRecorder.RecordingStopped -= OnRecordingStopped;
        }

        private void KeyInputEventComplete()
        {
            _inKeyInputEvent = false;

            try
            {
                if (!String.IsNullOrEmpty(_message))
                {
                    _margin.StatusLine = _message;
                }
                else if (_modeSwitch != null)
                {
                    UpdateForSwitchMode(_modeSwitch);
                }
                else
                {
                    UpdateForNoEvent();
                }
            }
            finally
            {
                _message = null;
                _modeSwitch = null;
            }
        }

        private void MessageEvent(string message)
        {
            if (_inKeyInputEvent)
            {
                _message = message;
            }
            else
            {
                _margin.StatusLine = message;
            }
        }

        private void UpdateForSwitchMode(IMode mode)
        {
            /// Calculate the argument string if we are in one time command mode
            string oneTimeArgument = null;
            if (_buffer.InOneTimeCommand.IsSome())
            {
                if (_buffer.InOneTimeCommand.Is(ModeKind.Insert))
                {
                    oneTimeArgument = "insert";
                }
                else if (_buffer.InOneTimeCommand.Is(ModeKind.Replace))
                {
                    oneTimeArgument = "replace";
                }
            }

            switch (mode.ModeKind)
            {
                case ModeKind.Normal:
                    _margin.StatusLine = String.IsNullOrEmpty(oneTimeArgument)
                        ? String.Empty
                        : String.Format(Resources.NormalOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.Command:
                    _margin.StatusLine = ":" + _buffer.CommandMode.Command;
                    break;
                case ModeKind.Insert:
                    _margin.StatusLine = Resources.InsertBanner;
                    break;
                case ModeKind.Replace:
                    _margin.StatusLine = Resources.ReplaceBanner;
                    break;
                case ModeKind.VisualBlock:
                    _margin.StatusLine = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualBlockBanner
                        : String.Format(Resources.VisualBlockOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualCharacter:
                    _margin.StatusLine = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualCharacterBanner
                        : String.Format(Resources.VisualCharacterOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.VisualLine:
                    _margin.StatusLine = String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualLineBanner
                        : String.Format(Resources.VisualLineOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.Select:
                    _margin.StatusLine = Resources.SelectBanner;
                    break;
                case ModeKind.ExternalEdit:
                    _margin.StatusLine = Resources.ExternalEditBanner;
                    break;
                case ModeKind.Disabled:
                    _margin.StatusLine = _buffer.DisabledMode.HelpMessage;
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                default:
                    _margin.StatusLine = String.Empty;
                    break;
            }
        }

        /// <summary>
        /// Update the status line at the end of a key press event which didn't result in 
        /// a mode change
        /// </summary>
        private void UpdateForNoEvent()
        {
            var search = _buffer.IncrementalSearch;
            if (search.InSearch && search.CurrentSearchData.IsSome())
            {
                var data = search.CurrentSearchData.Value;
                var prefix = data.Kind.IsAnyForward ? "/" : "?";
                _margin.StatusLine = prefix + data.Pattern;
                return;
            }

            switch (_buffer.ModeKind)
            {
                case ModeKind.Command:
                    _margin.StatusLine = ":" + _buffer.CommandMode.Command;
                    break;
                case ModeKind.Normal:
                    _margin.StatusLine = _buffer.NormalMode.Command;
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                case ModeKind.Disabled:
                    _margin.StatusLine = _buffer.DisabledMode.HelpMessage;
                    break;
                case ModeKind.VisualBlock:
                    _margin.StatusLine = Resources.VisualBlockBanner;
                    break;
                case ModeKind.VisualCharacter:
                    _margin.StatusLine = Resources.VisualCharacterBanner;
                    break;
                case ModeKind.VisualLine:
                    _margin.StatusLine = Resources.VisualLineBanner;
                    break;
            }
        }

        private void UpdateForRecordingChanged()
        {
            _margin.IsRecording = _buffer.Vim.MacroRecorder.IsRecording
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateSubstituteConfirmMode()
        {
            var replace = _buffer.SubstituteConfirmMode.CurrentSubstitute.SomeOrDefault("");
            _margin.StatusLine = String.Format(Resources.SubstituteConfirmBannerFormat, replace);
        }

        /// <summary>
        /// Update the color of the editor portion of the command window to be the user
        /// defined values
        /// </summary>
        private void UpdateTextColor()
        {
            var propertyMap = _editorFormatMap.GetProperties(CommandMarginFormatDefinition.Name);
            _margin.TextForeground = propertyMap.GetForegroundBrush(SystemColors.WindowTextBrush);
            _margin.TextForeground = propertyMap.GetForegroundBrush(SystemColors.WindowBrush);
        }

        #region Event Handlers

        private void OnSwitchMode(object sender, SwitchModeEventArgs args)
        {
            if (_inKeyInputEvent)
            {
                _modeSwitch = args.CurrentMode;
            }
            else
            {
                UpdateForSwitchMode(args.CurrentMode);
            }
        }

        private void OnKeyInputStart(object sender, KeyInputEventArgs args)
        {
            _inKeyInputEvent = true;
        }

        private void OnKeyInputEnd(object sender, KeyInputEventArgs args)
        {
            KeyInputEventComplete();
        }

        private void OnStatusMessage(object sender, StringEventArgs args)
        {
            MessageEvent(args.Message);
        }

        private void OnErrorMessage(object sender, StringEventArgs args)
        {
            MessageEvent(args.Message);
        }

        private void OnWarningMessage(object sender, StringEventArgs args)
        {
            MessageEvent(args.Message);
        }

        private void OnOptionsClicked(object sender, EventArgs e)
        {
            var provider = _optionsProviderFactory.Select(x => x.Value.CreateOptionsProvider()).Where(x => x != null).FirstOrDefault();
            if (provider != null)
            {
                provider.ShowDialog(_buffer);
            }
            else
            {
                MessageBox.Show("No options provider available");
            }
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            UpdateTextColor();
        }

        private void OnRecordingStarted(object sender, RecordRegisterEventArgs args)
        {
            UpdateForRecordingChanged();
        }

        private void OnRecordingStopped(object sender, EventArgs e)
        {
            UpdateForRecordingChanged();
        }

        #endregion
    }
}
