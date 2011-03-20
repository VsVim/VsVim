using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;

namespace Vim.UI.Wpf
{
    internal sealed class CommandMarginController
    {
        private readonly IVimBuffer _buffer;
        private readonly CommandMarginControl _margin;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactory;
        private bool _inKeyInputEvent;
        private string _message;
        private IMode _modeSwitch;

        internal CommandMarginController(IVimBuffer buffer, CommandMarginControl control, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactory)
        {
            _buffer = buffer;
            _margin = control;
            _optionsProviderFactory = optionsProviderFactory.ToList().AsReadOnly();

            _buffer.SwitchedMode += OnSwitchMode;
            _buffer.KeyInputStart += OnKeyInputStart;
            _buffer.KeyInputEnd += OnKeyInputEnd;
            _buffer.StatusMessage += OnStatusMessage;
            _buffer.StatusMessageLong += OnStatusMessageLong;
            _buffer.ErrorMessage += OnErrorMessage;
            _buffer.WarningMessage += OnWarningMessage;
            _buffer.Vim.MacroRecorder.RecordingStarted += delegate { UpdateForRecordingChanged(); };
            _buffer.Vim.MacroRecorder.RecordingStopped += delegate { UpdateForRecordingChanged(); };
            _margin.OptionsClicked += OnOptionsClicked;
            UpdateForRecordingChanged();
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
            switch (mode.ModeKind)
            {
                case ModeKind.Normal:
                    _margin.StatusLine = _buffer.NormalMode.OneTimeMode.Is(ModeKind.Insert)
                        ? Resources.PendingInsertBanner
                        : String.Empty;
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
                    _margin.StatusLine = Resources.VisualBlockBanner;
                    break;
                case ModeKind.VisualCharacter:
                    _margin.StatusLine = Resources.VisualCharacterBanner;
                    break;
                case ModeKind.VisualLine:
                    _margin.StatusLine = Resources.VisualLineBanner;
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

        private void UpdateForNoEvent()
        {
            switch (_buffer.ModeKind)
            {
                case ModeKind.Command:
                    _margin.StatusLine = ":" + _buffer.CommandMode.Command;
                    break;
                case ModeKind.Normal:
                    {
                        var mode = _buffer.NormalMode;
                        var search = _buffer.IncrementalSearch;
                        if (search.InSearch && search.CurrentSearch.IsSome())
                        {
                            var data = search.CurrentSearch.Value;
                            var prefix = data.Kind.IsAnyForward ? "/" : "?";
                            _margin.StatusLine = prefix + data.Text.RawText;
                        }
                        else
                        {
                            _margin.StatusLine = mode.Command;
                        }
                    }
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                case ModeKind.Disabled:
                    _margin.StatusLine = _buffer.DisabledMode.HelpMessage;
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

        private void OnKeyInputStart(object sender, KeyInput input)
        {
            _inKeyInputEvent = true;
        }

        private void OnKeyInputEnd(object sender, KeyInput input)
        {
            KeyInputEventComplete();
        }

        private void OnStatusMessage(object sender, string message)
        {
            MessageEvent(message);
        }

        private void OnStatusMessageLong(object sender, IEnumerable<string> lines)
        {
            var message = lines.Aggregate((x, y) => x + Environment.NewLine + y);
            MessageEvent(message);
        }

        private void OnErrorMessage(object sender, string message)
        {
            MessageEvent(message);
        }

        private void OnWarningMessage(object sender, string message)
        {
            MessageEvent(message);
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

        #endregion
    }
}
