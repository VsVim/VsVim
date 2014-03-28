using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Classification;
using Vim.Extensions;
using Vim.UI.Wpf.Properties;
using WpfKeyboard = System.Windows.Input.Keyboard;
using System.Text;

namespace Vim.UI.Wpf.Implementation.CommandMargin
{
    /// <summary>
    /// The type of edit that we are currently performing.  None exists when no command line edit
    /// </summary>
    internal enum EditKind
    {
        None,
        Command,
        SearchForward,
        SearchBackward
    }

    internal enum EditPosition
    {
        Start,
        End,
        BeforeLastCharacter
    }

    internal sealed class CommandMarginController
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginControl _margin;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IFontProperties _fontProperties;
        private readonly FrameworkElement _parentVisualElement;
        private bool _inKeyInputEvent;
        private bool _inCommandUpdate;
        private string _message;
        private SwitchModeEventArgs _modeSwitchEventArgs;
        private EditKind _editKind;

        /// <summary>
        /// We need to hold a reference to Text Editor visual element.
        /// </summary>
        internal FrameworkElement ParentVisualElement
        {
            get { return _parentVisualElement; }
        }

        internal EditKind CommandLineEditKind
        {
            get { return _editKind; }
        }

        internal CommandMarginController(IVimBuffer buffer, FrameworkElement parentVisualElement, CommandMarginControl control, IEditorFormatMap editorFormatMap, IFontProperties fontProperties)
        {
            _vimBuffer = buffer;
            _margin = control;
            _parentVisualElement = parentVisualElement;
            _editorFormatMap = editorFormatMap;
            _fontProperties = fontProperties;

            _vimBuffer.SwitchedMode += OnSwitchMode;
            _vimBuffer.KeyInputStart += OnKeyInputStart;
            _vimBuffer.KeyInputEnd += OnKeyInputEnd;
            _vimBuffer.StatusMessage += OnStatusMessage;
            _vimBuffer.ErrorMessage += OnErrorMessage;
            _vimBuffer.WarningMessage += OnWarningMessage;
            _vimBuffer.CommandMode.CommandChanged += OnCommandChanged;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted += OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped += OnRecordingStopped;
            _margin.Loaded += OnCommandMarginLoaded;
            _margin.Unloaded += OnCommandMarginUnloaded;
            _margin.CommandLineTextBox.PreviewKeyDown += OnCommandLineTextBoxPreviewKeyDown;
            _margin.CommandLineTextBox.TextChanged += OnCommandLineTextBoxTextChanged;
            _margin.CommandLineTextBox.SelectionChanged += OnCommandLineTextBoxSelectionChanged;
            _margin.CommandLineTextBox.LostKeyboardFocus += OnCommandLineTextBoxLostKeyboardFocus;
            _margin.CommandLineTextBox.PreviewMouseDown += OnCommandLineTextBoxPreviewMouseDown;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
            UpdateForRecordingChanged();
            UpdateTextColor();
        }

        private void ChangeEditKind(EditKind editKind)
        {
            if (editKind == _editKind)
            {
                return;
            }

            _editKind = editKind;
            switch (editKind)
            {
                case EditKind.None:
                    // Make sure that the editor has focus 
                    if (ParentVisualElement != null)
                    {
                        ParentVisualElement.Focus();
                    }
                    _margin.IsEditReadOnly = true;
                    break;
                case EditKind.Command:
                case EditKind.SearchForward:
                case EditKind.SearchBackward:
                    WpfKeyboard.Focus(_margin.CommandLineTextBox);
                    _margin.IsEditReadOnly = false;
                    break;
                default:
                    Contract.FailEnumValue(editKind);
                    break;
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
                else if (_modeSwitchEventArgs != null)
                {
                    UpdateForSwitchMode(_modeSwitchEventArgs.PreviousMode, _modeSwitchEventArgs.CurrentMode);
                }
                else
                {
                    UpdateForNoEvent();
                }
            }
            finally
            {
                _message = null;
                _modeSwitchEventArgs = null;
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

        private void UpdateForSwitchMode(IMode previousMode, IMode currentMode)
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

            switch (currentMode.ModeKind)
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
            // In the middle of an edit the edit edit box is responsible for keeping the 
            // text up to date 
            if (_editKind != EditKind.None)
            {
                return;
            }

            var search = _vimBuffer.IncrementalSearch;
            if (search.InSearch)
            {
                _margin.StatusLine = search.CurrentSearchText;
                return;
            }

            switch (_vimBuffer.ModeKind)
            {
                case ModeKind.Command:
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
        /// Update the font family and size of the command margin text input controls
        /// </summary>
        private void UpdateFontProperties()
        {
            _margin.TextFontFamily = _fontProperties.FontFamily;

            // Convert points (1 pt = 1/72") to pixels (1 WPF pixel = 1/96").
            _margin.TextFontSize = _fontProperties.FontSize * 96 / 72;
        }

        /// <summary>
        /// This method handles the KeyInput as it applies to command line editor.  Make sure to 
        /// mark the key as handled if we use it here.  If we don't then it will propagate out to 
        /// the editor and be processed again
        /// </summary>
        internal void HandleKeyEvent(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                    ChangeEditKind(EditKind.None);
                    e.Handled = true;
                    break;
                case Key.Return:
                    ExecuteCommand(_margin.CommandLineTextBox.Text);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _vimBuffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
                    e.Handled = true;
                    break;
                case Key.Down:
                    _vimBuffer.Process(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// If we're in command mode and a key is processed which effects the edit we should handle
        /// it here
        /// </summary>
        private void CheckEnableCommandLineEdit(KeyInputStartEventArgs args)
        {
            if (_editKind != EditKind.None)
            {
                return;
            }

            var commandLineEditKind = CalculateCommandLineEditKind();
            if (commandLineEditKind == EditKind.None)
            {
                return;
            }

            switch (args.KeyInput.Key)
            {
                case VimKey.Home:
                    // Enable command line edition
                    ChangeEditKind(commandLineEditKind);
                    _margin.UpdateCaretPosition(EditPosition.Start);
                    args.Handled = true;
                    break;
                case VimKey.Left:
                    ChangeEditKind(commandLineEditKind);
                    _margin.UpdateCaretPosition(EditPosition.BeforeLastCharacter);
                    args.Handled = true;
                    break;
                case VimKey.Up:
                case VimKey.Down:
                    // User is navigation through history, move caret to the end of the entry
                    _margin.UpdateCaretPosition(EditPosition.End);
                    break;
            }
        }

        /// <summary>
        /// Update the current command from the given input
        /// </summary>
        private void UpdateCommand(string input)
        {
            _inCommandUpdate = true;
            try
            {
                input = input ?? "";
                switch (_editKind)
                {
                    case EditKind.Command:

                        if (_vimBuffer.ModeKind == ModeKind.Command)
                        {
                            var command = input.Length > 0 && input[0] == ':'
                                ? input.Substring(1)
                                : input;
                            _vimBuffer.CommandMode.Command = command;
                        }
                        break;
                    case EditKind.SearchBackward:
                        if (_vimBuffer.IncrementalSearch.InSearch)
                        {
                            var pattern = input.Length > 0 && input[0] == '?'
                                ? input.Substring(1)
                                : input;
                            _vimBuffer.IncrementalSearch.ResetSearch(pattern);
                        }
                        break;
                    case EditKind.SearchForward:
                        if (_vimBuffer.IncrementalSearch.InSearch)
                        {
                            var pattern = input.Length > 0 && input[0] == '/'
                                ? input.Substring(1)
                                : input;
                            _vimBuffer.IncrementalSearch.ResetSearch(pattern);
                        }
                        break;
                    case EditKind.None:
                        break;
                    default:
                        Contract.FailEnumValue(_editKind);
                        break;
                }
            }
            finally
            {
                _inCommandUpdate = false;
            }
        }

        /// <summary>
        /// Execute the command and switch focus back to the editor
        /// </summary>
        private void ExecuteCommand(string command)
        {
            if (_editKind == EditKind.None)
            {
                return;
            }

            ChangeEditKind(EditKind.None);
            UpdateCommand(command);
            _vimBuffer.Process(KeyInputUtil.EnterKey);
        }

        #region Event Handlers

        private void OnSwitchMode(object sender, SwitchModeEventArgs args)
        {
            if (_inKeyInputEvent)
            {
                _modeSwitchEventArgs = args;
            }
            else
            {
                UpdateForSwitchMode(args.PreviousMode, args.CurrentMode);
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

        private void OnCommandMarginLoaded(object sender, RoutedEventArgs e)
        {
            _fontProperties.FontPropertiesChanged += OnFontPropertiesChanged;
            UpdateFontProperties();
        }

        private void OnCommandMarginUnloaded(object sender, RoutedEventArgs e)
        {
            _fontProperties.FontPropertiesChanged -= OnFontPropertiesChanged;
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            UpdateTextColor();
        }

        private void OnFontPropertiesChanged(object sender, FontPropertiesEventArgs e)
        {
            UpdateFontProperties();
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
            if (_inCommandUpdate)
            {
                return;
            }

            UpdateForNoEvent();
        }

        private void OnCommandLineTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyEvent(e);
        }

        private void OnCommandLineTextBoxTextChanged(object sender, RoutedEventArgs e)
        {
            // If we are in an edit mode make sure the user didn't delete the command prefix 
            // from the edit box 
            var command = _margin.CommandLineTextBox.Text;
            var prefixChar = GetPrefixChar(_editKind);
            if (prefixChar != null)
            {
                bool update = false;
                if (String.IsNullOrEmpty(command))
                {
                    command = prefixChar.Value.ToString();
                    update = true;
                }

                if (command[0] != prefixChar.Value)
                {
                    command = prefixChar.Value.ToString() + command;
                    update = true;
                }

                // If there is an update requested then change the text and immediately return.  The change
                // of text will cause this function to be re-entered since it's an event handler for 
                // text change.  Hence return because the new command has already been processed
                if (update)
                {
                    _margin.CommandLineTextBox.Text = command;
                    _margin.CommandLineTextBox.Select(1, 0);
                    return;
                }
            }

            UpdateCommand(command);
        }

        /// <summary>
        /// If the user selects the text with the mouse then we need to initiate an edit 
        /// in the case the vim buffer is capable of one.  If not then we need to cancel
        /// the selection.  Anything else will give the user the appearance that they can
        /// edit the text when in fact they cannot
        /// </summary>
        private void OnCommandLineTextBoxSelectionChanged(object sender, RoutedEventArgs e)
        {
            var textBox = _margin.CommandLineTextBox;
            if (string.IsNullOrEmpty(textBox.SelectedText))
            {
                return;
            }

            var kind = CalculateCommandLineEditKind();
            if (kind != EditKind.None)
            {
                ChangeEditKind(kind);
            }
        }

        private void OnCommandLineTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ChangeEditKind(EditKind.None);
        }

        /// <summary>
        /// If the user clicks on the edit box then consider switching to edit mode
        /// </summary>
        private void OnCommandLineTextBoxPreviewMouseDown(object sender, RoutedEventArgs e)
        {
            if (_editKind != EditKind.None)
            {
                return;
            }

            var commandLineEditKind = CalculateCommandLineEditKind();
            if (commandLineEditKind == EditKind.None)
            {
                return;
            }

            ChangeEditKind(commandLineEditKind);
            _margin.UpdateCaretPosition(EditPosition.End);
            e.Handled = true;
        }

        /// <summary>
        /// Calculate the type of edit that should be performed based on the current state of the
        /// IVimBuffer
        /// </summary>
        private EditKind CalculateCommandLineEditKind()
        {
            if (_vimBuffer.ModeKind == ModeKind.Command)
            {
                return EditKind.Command;
            }

            if (_vimBuffer.IncrementalSearch.InSearch)
            {
                return _vimBuffer.IncrementalSearch.CurrentSearchData.Kind.IsAnyForward
                    ? EditKind.SearchForward
                    : EditKind.SearchBackward;
            }

            return EditKind.None;
        }

        private static char? GetPrefixChar(EditKind editKind)
        {
            switch (editKind)
            {
                case EditKind.None:
                    return null;
                case EditKind.Command:
                    return ':';
                case EditKind.SearchForward:
                    return '/';
                case EditKind.SearchBackward:
                    return '?';
                default:
                    Contract.FailEnumValue(editKind);
                    return null;
            }
        }

        #endregion
    }
}
