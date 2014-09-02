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
using System.Text;
using System.Diagnostics;
using WpfKeyboard = System.Windows.Input.Keyboard;
using WpfTextChangedEventArgs = System.Windows.Controls.TextChangedEventArgs;

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
        /// <summary>
        /// Captures the changes which occur during a key press event.  Essentially the information
        /// between the start and end key event in Vim 
        /// </summary>
        private struct VimBufferKeyEventState
        {
            internal int KeyInputEventCount;

            /// <summary>
            /// Stores any messages that occurred in the buffer (warnings, errors, etc ...) 
            /// </summary>
            internal string Message;

            /// <summary>
            /// Stores the last switch mode which occurred during the key event 
            /// </summary>
            internal SwitchModeEventArgs SwitchModeEventArgs;

            internal bool InEvent
            {
                get { return KeyInputEventCount > 0; }
            }

            internal void Clear()
            {
                Message = null;
                SwitchModeEventArgs = null;
            }
        }

        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginControl _margin;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly FrameworkElement _parentVisualElement;
        private VimBufferKeyEventState _vimBufferKeyEventState;
        private bool _inUpdateVimBufferState;
        private bool _inCommandLineUpdate;
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

        /// <summary>
        /// Are we in the middle of a key press sequence?  
        /// </summary>
        internal bool InVimBufferKeyEvent
        {
            get { return _vimBufferKeyEventState.InEvent; }
        }

        internal bool InCommandLineUpdate
        {
            get { return _inCommandLineUpdate; }
        }

        internal bool InPasteWait
        {
            get
            {
                if (_vimBuffer.ModeKind == ModeKind.Command)
                {
                    return _vimBuffer.CommandMode.InPasteWait;
                }

                var search = _vimBuffer.IncrementalSearch;
                if (search.InSearch && search.InPasteWait)
                {
                    return true;
                }

                return false;
            }
        }

        internal CommandMarginController(IVimBuffer buffer, FrameworkElement parentVisualElement, CommandMarginControl control, IEditorFormatMap editorFormatMap, IClassificationFormatMap classificationFormatMap)
        {
            _vimBuffer = buffer;
            _margin = control;
            _parentVisualElement = parentVisualElement;
            _editorFormatMap = editorFormatMap;
            _classificationFormatMap = classificationFormatMap;

            _vimBuffer.SwitchedMode += OnSwitchMode;
            _vimBuffer.KeyInputStart += OnKeyInputStart;
            _vimBuffer.KeyInputEnd += OnKeyInputEnd;
            _vimBuffer.StatusMessage += OnStatusMessage;
            _vimBuffer.ErrorMessage += OnErrorMessage;
            _vimBuffer.WarningMessage += OnWarningMessage;
            _vimBuffer.CommandMode.CommandChanged += OnCommandModeCommandChanged;
            _vimBuffer.TextView.GotAggregateFocus += OnGotAggregateFocus;
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

        void OnGotAggregateFocus(object sender, EventArgs e)
        {
            UpdateStatusLine();
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
                    UpdateForNoEvent();
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
            _vimBuffer.CommandMode.CommandChanged -= OnCommandModeCommandChanged;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted -= OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped -= OnRecordingStopped;
        }

        private void KeyInputEventComplete()
        {
            Debug.Assert(_vimBufferKeyEventState.InEvent);
            try
            {
                if (!String.IsNullOrEmpty(_vimBufferKeyEventState.Message))
                {
                    UpdateCommandLine(_vimBufferKeyEventState.Message);
                }
                else if (_vimBufferKeyEventState.SwitchModeEventArgs != null)
                {
                    var args = _vimBufferKeyEventState.SwitchModeEventArgs;
                    UpdateForSwitchMode(args.PreviousMode, args.CurrentMode);
                }
                else
                {
                    UpdateForNoEvent();
                }
                UpdateStatusLine();
            }
            finally
            {
                _vimBufferKeyEventState.KeyInputEventCount--;
                _vimBufferKeyEventState.Clear();
            }
        }

        private void MessageEvent(string message)
        {
            if (_vimBufferKeyEventState.InEvent)
            {
                _vimBufferKeyEventState.Message = message;
            }
            else
            {
                UpdateCommandLine(message);
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
                    UpdateCommandLine(String.IsNullOrEmpty(oneTimeArgument)
                        ? String.Empty
                        : String.Format(Resources.NormalOneTimeCommandBanner, oneTimeArgument));
                    break;
                case ModeKind.Command:
                    UpdateCommandLine(":" + _vimBuffer.CommandMode.Command);
                    break;
                case ModeKind.Insert:
                    UpdateCommandLine(Resources.InsertBanner);
                    break;
                case ModeKind.Replace:
                    UpdateCommandLine(Resources.ReplaceBanner);
                    break;
                case ModeKind.VisualBlock:
                    UpdateCommandLine(String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualBlockBanner
                        : String.Format(Resources.VisualBlockOneTimeCommandBanner, oneTimeArgument));
                    break;
                case ModeKind.VisualCharacter:
                    UpdateCommandLine(String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualCharacterBanner
                        : String.Format(Resources.VisualCharacterOneTimeCommandBanner, oneTimeArgument));
                    break;
                case ModeKind.VisualLine:
                    UpdateCommandLine(String.IsNullOrEmpty(oneTimeArgument)
                        ? Resources.VisualLineBanner
                        : String.Format(Resources.VisualLineOneTimeCommandBanner, oneTimeArgument));
                    break;
                case ModeKind.SelectBlock:
                    UpdateCommandLine(Resources.SelectBlockBanner);
                    break;
                case ModeKind.SelectCharacter:
                    UpdateCommandLine(Resources.SelectCharacterBanner);
                    break;
                case ModeKind.SelectLine:
                    UpdateCommandLine(Resources.SelectLineBanner);
                    break;
                case ModeKind.ExternalEdit:
                    UpdateCommandLine(Resources.ExternalEditBanner);
                    break;
                case ModeKind.Disabled:
                    UpdateCommandLine(_vimBuffer.DisabledMode.HelpMessage);
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                default:
                    UpdateCommandLine(String.Empty);
                    break;
            }
        }

        /// <summary>
        /// Update the status in command line at the end of a key press event which didn't result in 
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
                var searchText = search.CurrentSearchText;
                var prefix = search.CurrentSearchData.Path.IsForward ? "/" : "?";
                if (InPasteWait)
                {
                    searchText += "\"";
                }
                UpdateCommandLine(prefix + searchText);
                return;
            }

            switch (_vimBuffer.ModeKind)
            {
                case ModeKind.Command:
                    UpdateCommandLine(":" + _vimBuffer.CommandMode.Command + (InPasteWait ? "\"" : ""));
                    break;
                case ModeKind.Normal:
                    UpdateCommandLine(_vimBuffer.NormalMode.Command);
                    break;
                case ModeKind.SubstituteConfirm:
                    UpdateSubstituteConfirmMode();
                    break;
                case ModeKind.Disabled:
                    UpdateCommandLine(_vimBuffer.DisabledMode.HelpMessage);
                    break;
                case ModeKind.VisualBlock:
                    UpdateCommandLine(Resources.VisualBlockBanner);
                    break;
                case ModeKind.VisualCharacter:
                    UpdateCommandLine(Resources.VisualCharacterBanner);
                    break;
                case ModeKind.VisualLine:
                    UpdateCommandLine(Resources.VisualLineBanner);
                    break;
            }
        }

        private void UpdateForRecordingChanged()
        {
            _margin.IsRecording = _vimBuffer.Vim.MacroRecorder.IsRecording
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Update the status line.
        /// </summary>
        private void UpdateStatusLine()
        {
            var isStatusLineVisible = _vimBuffer.GlobalSettings.LastStatus != 0;

            _margin.IsStatuslineVisible = isStatusLineVisible
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (isStatusLineVisible)
            {
                var statusLineFormat = _vimBuffer.GlobalSettings.StatusLine;
                _margin.StatusLine = statusLineFormat;
            }
        }

        private void UpdateSubstituteConfirmMode()
        {
            var replace = _vimBuffer.SubstituteConfirmMode.CurrentSubstitute.SomeOrDefault("");
            UpdateCommandLine(String.Format(Resources.SubstituteConfirmBannerFormat, replace));
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
            _margin.TextFontFamily = _classificationFormatMap.DefaultTextProperties.Typeface.FontFamily;
            _margin.TextFontSize = _classificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
        }

        /// <summary>
        /// This is the one and only function which should be updating the command line being displayed
        /// to the user.  Having a single function to perform this allows us to distinguish between edits
        /// from the user and mere messaging changes coming from vim events
        /// </summary>
        private void UpdateCommandLine(string commandLine)
        {
            Debug.Assert(!_inCommandLineUpdate);
            _inCommandLineUpdate = true;
            try
            {
                _margin.CommandLineTextBox.Text = commandLine;
            }
            finally
            {
                _inCommandLineUpdate = false;
            }
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
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        // During edits we are responsible for handling the command line.  Need to 
                        // put a " into the box at the edit position
                        var textBox = _margin.CommandLineTextBox;
                        var text = textBox.Text;
                        var builder = new StringBuilder();
                        var offset = textBox.SelectionStart;
                        builder.Append(text, 0, offset);
                        builder.Append('"');
                        builder.Append(text, offset, text.Length - offset);
                        UpdateCommandLine(builder.ToString());
                        textBox.Select(offset, 0);

                        // Now move the buffer into paste wait 
                        _vimBuffer.Process(KeyInputUtil.ApplyModifiersToChar('r', KeyModifiers.Control));
                    }
                    break;
                case Key.U:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        var textBox = _margin.CommandLineTextBox;
                        var text = textBox.Text.Substring(textBox.SelectionStart);
                        textBox.Text = text;

                        UpdateVimBufferStateWithCommandText(text);
                    }
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
        internal void UpdateVimBufferStateWithCommandText(string commandText)
        {
            _inUpdateVimBufferState = true;
            try
            {
                commandText = commandText ?? "";
                var prefixChar = GetPrefixChar(_editKind);
                if (prefixChar.HasValue && commandText.Length > 0 && commandText[0] == prefixChar.Value)
                {
                    commandText = commandText.Substring(1);
                }

                switch (_editKind)
                {
                    case EditKind.Command:
                        if (_vimBuffer.ModeKind == ModeKind.Command)
                        {
                            _vimBuffer.CommandMode.Command = commandText;
                        }
                        break;
                    case EditKind.SearchBackward:
                    case EditKind.SearchForward:
                        if (_vimBuffer.IncrementalSearch.InSearch)
                        {
                            _vimBuffer.IncrementalSearch.ResetSearch(commandText);
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
                _inUpdateVimBufferState = false;
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
            UpdateVimBufferStateWithCommandText(command);
            _vimBuffer.Process(KeyInputUtil.EnterKey);
        }

        private void OnSwitchMode(object sender, SwitchModeEventArgs args)
        {
            if (InVimBufferKeyEvent)
            {
                _vimBufferKeyEventState.SwitchModeEventArgs = args;
            }
            else
            {
                UpdateForSwitchMode(args.PreviousMode, args.CurrentMode);
            }
        }

        private void OnKeyInputStart(object sender, KeyInputStartEventArgs args)
        {
            _vimBufferKeyEventState.KeyInputEventCount++;
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
            _classificationFormatMap.ClassificationFormatMappingChanged += OnFontPropertiesChanged;
            UpdateFontProperties();
        }

        private void OnCommandMarginUnloaded(object sender, RoutedEventArgs e)
        {
            _classificationFormatMap.ClassificationFormatMappingChanged -= OnFontPropertiesChanged;
        }

        private void OnFormatMappingChanged(object sender, FormatItemsEventArgs e)
        {
            UpdateTextColor();
        }

        private void OnFontPropertiesChanged(object sender, EventArgs e)
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

        private void OnCommandModeCommandChanged(object sender, EventArgs e)
        {
            // It is completely expected for the command mode command to change while we are updating
            // the vim buffer state.  
            if (_inUpdateVimBufferState)
            {
                return;
            }

            UpdateForNoEvent();
        }

        private void OnCommandLineTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyEvent(e);
        }

        private void OnCommandLineTextBoxTextChanged(object sender, WpfTextChangedEventArgs e)
        {
            // If the update is being made by the control for the purpose of displaying a message
            // then we do not want or need to respond to this event 
            if (_inCommandLineUpdate)
            {
                return;
            }

            if (InPasteWait)
            {
                UpdateForPasteWait(e);
                return;
            }

            // If we are in an edit mode make sure the user didn't delete the command prefix 
            // from the edit box 
            var command = _margin.CommandLineTextBox.Text ?? "";
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

                if (update)
                {
                    UpdateCommandLine(command);
                }
            }

            UpdateVimBufferStateWithCommandText(command);
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

        private void UpdateForPasteWait(WpfTextChangedEventArgs e)
        {
            Debug.Assert(InPasteWait);

            var command = _margin.CommandLineTextBox.Text ?? "";
            if (e.Changes.Count == 1 && command.Length > 0)
            {
                var change = e.Changes.First();
                if (change.AddedLength == 1)
                {
                    // If we are in a paste wait context then attempt to complete it by passing on the 
                    // typed char to _vimBuffer.  This will process it as the register
                    var c = command[change.Offset];
                    var keyInput = KeyInputUtil.CharToKeyInput(c);
                    _vimBuffer.Process(keyInput);

                    // Now we need to update the command line.  During edits the controller is responsible
                    // for manually updating the command line state.  Also we have to keep the caret postion
                    // correct
                    var name = RegisterName.OfChar('c');
                    if (name.IsSome())
                    {
                        var toPaste = _vimBuffer.GetRegister(name.Value).StringValue;
                        var builder = new StringBuilder();
                        builder.Append(command, 0, change.Offset);
                        builder.Append(toPaste);
                        builder.Append(command, change.Offset + 2, command.Length - (change.Offset + 2));
                        _margin.CommandLineTextBox.Text = builder.ToString();
                        _margin.CommandLineTextBox.Select(change.Offset + toPaste.Length, 0);
                    }

                    return;
                }
            }

            // The buffer was in a paste wait but the UI isn't in sync for completing
            // the operation.  Just pass Escape down to the buffer so it will cancel out
            // of paste wait and go back to a known state
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
        }
    }
}
