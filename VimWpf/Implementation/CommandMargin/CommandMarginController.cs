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
            _buffer.CommandMode.CommandChanged += OnCommandChanged;
            _buffer.Vim.MacroRecorder.RecordingStarted += OnRecordingStarted;
            _buffer.Vim.MacroRecorder.RecordingStopped += OnRecordingStopped;
            _margin.OptionsClicked += OnOptionsClicked;
            _margin.CancelCommandEdition += OnCancelCommandEdition;
            _margin.RunCommandEdition += OnRunCommandEdition;
            _margin.HistoryGoPrevious += OnHistoryGoPrevious;
            _margin.HistoryGoNext += OnHistoryGoNext;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
            UpdateForRecordingChanged();
            UpdateTextColor();
        }

        /// <summary>
        /// We need to hold a reference to Text Editor visual element.
        /// TODO: maybe this property could be available through IVimBuffer.
        /// </summary>
        public FrameworkElement ParentVisualElement { get; set; }

        private void FocusEditor()
        {
            if (null != ParentVisualElement)
            {
                ParentVisualElement.Focus();
            }
        }

        internal void Disconnect()
        {
            _buffer.CommandMode.CommandChanged -= OnCommandChanged;
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
            // Calculate the argument string if we are in one time command mode
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

            // Check if we can enable the command line to accept user input
            var search = _buffer.IncrementalSearch;
            _margin.IsCommandEditionDisable = mode.ModeKind != ModeKind.Command && !(search.InSearch && search.CurrentSearchData.IsSome());

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
                case ModeKind.SelectBlock:
                    _margin.StatusLine = Resources.SelectBlockBanner;
                    break;
                case ModeKind.SelectCharacter:
                    _margin.StatusLine = Resources.SelectCharacterBanner;
                    break;
                case ModeKind.SelectLine:
                    _margin.StatusLine = Resources.SelectLineBanner;
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
                //TODO: Workaround to fix strange character when pressing <Home>...
                _margin.StatusLine = prefix + data.Pattern.Trim('\0');
                _margin.IsCommandEditionDisable = false;
                return;
            }

            _margin.IsCommandEditionDisable = _buffer.ModeKind != ModeKind.Command;

            switch (_buffer.ModeKind)
            {
                case ModeKind.Command:
                    //TODO: Workaround to fix strange character when pressing <Home>...
                    _margin.StatusLine = ":" + _buffer.CommandMode.Command.Trim('\0'); ;
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
            _margin.TextBackground = propertyMap.GetBackgroundBrush(SystemColors.WindowBrush);
        }

        #region Event Handlers

        void OnHistoryGoPrevious(object sender, EventArgs e)
        {
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
        }

        void OnHistoryGoNext(object sender, EventArgs e)
        {
            _buffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
        }

        void OnCancelCommandEdition(object sender, EventArgs e)
        {
            _buffer.Process(KeyInputUtil.EscapeKey);

            FocusEditor();
        }

        void OnRunCommandEdition(object sender, EventArgs e)
        {
            _buffer.Process(KeyInputUtil.EscapeKey);

            var input = (e as CommandMarginEventArgs).Command;

            foreach (var c in input)
            {
                var i = KeyInputUtil.CharToKeyInput(c);
                _buffer.Process(i);
            }

            FocusEditor();
        }

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

            if (args.CurrentMode.ModeKind == ModeKind.Command)
            {
                FocusEditor();
            }
        }

        private void OnKeyInputStart(object sender, KeyInputEventArgs args)
        {
            _inKeyInputEvent = true;
        }

        private void OnKeyInputEnd(object sender, KeyInputEventArgs args)
        {
            KeyInputEventComplete();

            if (!_margin.IsCommandEditionDisable)
            {
                switch (args.KeyInput.Key)
                {
                    // Enable command line edition
                    case VimKey.Home:
                        _margin.FocusCommandLine(false);
                        break;
                    case VimKey.Left:
                        _margin.FocusCommandLine(true);
                        break;
                    // User is navigation through history, move caret to the end of the entry
                    case VimKey.Up:
                    case VimKey.Down:
                        _margin.UpdateCaretPosition(true);
                        break;
                }
            }
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

        private void OnCommandChanged(object sender, EventArgs e)
        {
            UpdateForNoEvent();
        }

        #endregion
    }
}
