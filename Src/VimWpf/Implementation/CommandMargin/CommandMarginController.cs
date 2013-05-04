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
        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginControl _margin;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly ReadOnlyCollection<Lazy<IOptionsProviderFactory>> _optionsProviderFactory;
        private readonly FrameworkElement _parentVisualElement;
        private bool _inKeyInputEvent;
        private string _message;
        private IMode _modeSwitch;

        /// <summary>
        /// We need to hold a reference to Text Editor visual element.
        /// </summary>
        public FrameworkElement ParentVisualElement
        {
            get { return _parentVisualElement; }
        }

        internal CommandMarginController(IVimBuffer buffer, FrameworkElement parentVisualElement, CommandMarginControl control, IEditorFormatMap editorFormatMap, IEnumerable<Lazy<IOptionsProviderFactory>> optionsProviderFactory)
        {
            _vimBuffer = buffer;
            _margin = control;
            _parentVisualElement = parentVisualElement;
            _editorFormatMap = editorFormatMap;
            _optionsProviderFactory = optionsProviderFactory.ToList().AsReadOnly();

            _vimBuffer.SwitchedMode += OnSwitchMode;
            _vimBuffer.KeyInputStart += OnKeyInputStart;
            _vimBuffer.KeyInputEnd += OnKeyInputEnd;
            _vimBuffer.StatusMessage += OnStatusMessage;
            _vimBuffer.ErrorMessage += OnErrorMessage;
            _vimBuffer.WarningMessage += OnWarningMessage;
            _vimBuffer.CommandMode.CommandChanged += OnCommandChanged;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted += OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped += OnRecordingStopped;
            _margin.OptionsClicked += OnOptionsClicked;
            _margin.CancelCommandEdition += OnCancelCommandEdition;
            _margin.RunCommandEdition += OnRunCommandEdition;
            _margin.HistoryGoPrevious += OnHistoryGoPrevious;
            _margin.HistoryGoNext += OnHistoryGoNext;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
            UpdateForRecordingChanged();
            UpdateTextColor();
        }

        private void FocusEditor()
        {
            if (null != ParentVisualElement)
            {
                ParentVisualElement.Focus();
            }
        }

        internal void Disconnect()
        {
            _vimBuffer.CommandMode.CommandChanged -= OnCommandChanged;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted -= OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped -= OnRecordingStopped;
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
            if (_vimBuffer.InOneTimeCommand.IsSome())
            {
                if (_vimBuffer.InOneTimeCommand.Is(ModeKind.Insert))
                {
                    oneTimeArgument = "insert";
                }
                else if (_vimBuffer.InOneTimeCommand.Is(ModeKind.Replace))
                {
                    oneTimeArgument = "replace";
                }
            }

            // Check if we can enable the command line to accept user input
            var search = _vimBuffer.IncrementalSearch;
            _margin.IsCommandEditionDisable = mode.ModeKind != ModeKind.Command && !(search.InSearch && search.CurrentSearchData.IsSome());

            switch (mode.ModeKind)
            {
                case ModeKind.Normal:
                    _margin.StatusLine = String.IsNullOrEmpty(oneTimeArgument)
                        ? String.Empty
                        : String.Format(Resources.NormalOneTimeCommandBanner, oneTimeArgument);
                    break;
                case ModeKind.Command:
                    _margin.StatusLine = ":" + _vimBuffer.CommandMode.Command;
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
                    _margin.StatusLine = _vimBuffer.DisabledMode.HelpMessage;
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
            var search = _vimBuffer.IncrementalSearch;
            if (search.InSearch && search.CurrentSearchData.IsSome())
            {
                var data = search.CurrentSearchData.Value;
                var prefix = data.Kind.IsAnyForward ? "/" : "?";
                //TODO: Workaround to fix strange character when pressing <Home>...
                _margin.StatusLine = prefix + data.Pattern.Trim('\0');
                _margin.IsCommandEditionDisable = false;
                return;
            }

            _margin.IsCommandEditionDisable = _vimBuffer.ModeKind != ModeKind.Command;

            switch (_vimBuffer.ModeKind)
            {
                case ModeKind.Command:
                    //TODO: Workaround to fix strange character when pressing <Home>...
                    _margin.StatusLine = ":" + _vimBuffer.CommandMode.Command.Trim('\0'); ;
                    break;
                case ModeKind.Normal:
                    _margin.StatusLine = _vimBuffer.NormalMode.Command;
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                case ModeKind.Disabled:
                    _margin.StatusLine = _vimBuffer.DisabledMode.HelpMessage;
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
            _margin.IsRecording = _vimBuffer.Vim.MacroRecorder.IsRecording
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateSubstituteConfirmMode()
        {
            var replace = _vimBuffer.SubstituteConfirmMode.CurrentSubstitute.SomeOrDefault("");
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

        /// <summary>
        /// If we're in command mode and a key is processed which effects the edit we should handle
        /// it here
        /// </summary>
        private void CheckEnableCommandLineEdit(KeyInputStartEventArgs args)
        {
            if (_vimBuffer.ModeKind != ModeKind.Command)
            {
                return;
            }

            switch (args.KeyInput.Key)
            {
                case VimKey.Home:
                    // Enable command line edition
                    _margin.FocusCommandLine(moveCaretToEnd: false);
                    args.Handled = true;
                    break;
                case VimKey.Left:
                    _margin.FocusCommandLine(moveCaretToEnd: true);
                    args.Handled = true;
                    break;
                case VimKey.Up:
                case VimKey.Down:
                    // User is navigation through history, move caret to the end of the entry
                    _margin.UpdateCaretPosition(true);
                    break;
            }
        }

        #region Event Handlers

        void OnHistoryGoPrevious(object sender, EventArgs e)
        {
            _vimBuffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
        }

        void OnHistoryGoNext(object sender, EventArgs e)
        {
            _vimBuffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
        }

        void OnCancelCommandEdition(object sender, EventArgs e)
        {
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
            FocusEditor();
        }

        void OnRunCommandEdition(object sender, EventArgs e)
        {
            _vimBuffer.Process(KeyInputUtil.EscapeKey);

            var input = (e as CommandMarginEventArgs).Command;

            foreach (var c in input)
            {
                var i = KeyInputUtil.CharToKeyInput(c);
                _vimBuffer.Process(i);
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
        }

        private void OnKeyInputStart(object sender, KeyInputStartEventArgs args)
        {
            _inKeyInputEvent = true;
            CheckEnableCommandLineEdit(args);
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
                provider.ShowDialog(_vimBuffer);
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
