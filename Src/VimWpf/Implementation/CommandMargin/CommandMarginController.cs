using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            internal bool InEvent => KeyInputEventCount > 0;

            internal void Clear()
            {
                if (!InEvent)
                {
                    // This might be a nested key input event, for example
                    // if a macro is being replayed. Only clear the event
                    // state for the outermost key input event so we don't
                    // lose messages.
                    Message = null;
                }
                SwitchModeEventArgs = null;
            }
        }

        private readonly IVimBuffer _vimBuffer;
        private readonly CommandMarginControl _margin;
        private readonly IEditorFormatMap _editorFormatMap;
        private readonly IClassificationFormatMap _classificationFormatMap;
        private readonly ICommonOperations _commonOperations;
        private readonly IClipboardDevice _clipboardDevice;
        private readonly FrameworkElement _parentVisualElement;
        private readonly PasteWaitMemo _pasteWaitMemo = new PasteWaitMemo();
        private VimBufferKeyEventState _vimBufferKeyEventState;
        private bool _inUpdateVimBufferState;
        private bool _inCommandLineUpdate;
        private EditKind _editKind;
        private bool _processingVirtualKeyInputs;

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
                if (search.HasActiveSession && search.InPasteWait)
                {
                    return true;
                }

                return false;
            }
        }

        internal CommandMarginController(IVimBuffer buffer, FrameworkElement parentVisualElement, CommandMarginControl control, IEditorFormatMap editorFormatMap, IClassificationFormatMap classificationFormatMap, ICommonOperations commonOperations, IClipboardDevice clipboardDevice)
        {
            _vimBuffer = buffer;
            _margin = control;
            _parentVisualElement = parentVisualElement;
            _editorFormatMap = editorFormatMap;
            _classificationFormatMap = classificationFormatMap;
            _commonOperations = commonOperations;
            _clipboardDevice = clipboardDevice;

            Connect();
            UpdateForRecordingChanged();
            UpdateTextColor();
            UpdateStatusLineVisibility();
        }

        private void OnGotAggregateFocus(object sender, EventArgs e)
        {
            UpdateStatusLineVisibility();
        }

        private void ChangeEditKind(EditKind editKind, bool updateCommandLine = true)
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

                    if (updateCommandLine)
                    {
                        UpdateCommandLineForNoEvent();
                    }

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

        internal void Connect()
        {
            _vimBuffer.SwitchedMode += OnSwitchMode;
            _vimBuffer.KeyInputStart += OnKeyInputStart;
            _vimBuffer.KeyInputEnd += OnKeyInputEnd;
            _vimBuffer.StatusMessage += OnStatusMessage;
            _vimBuffer.ErrorMessage += OnErrorMessage;
            _vimBuffer.WarningMessage += OnWarningMessage;
            _vimBuffer.KeyInputProcessed += OnKeyInputProcessed;
            _vimBuffer.CommandMode.CommandChanged += OnCommandModeCommandChanged;
            _vimBuffer.TextView.GotAggregateFocus += OnGotAggregateFocus;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted += OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped += OnRecordingStopped;
            _margin.Loaded += OnCommandMarginLoaded;
            _margin.Unloaded += OnCommandMarginUnloaded;
            _margin.CommandLineTextBox.PreviewKeyDown += OnCommandLineTextBoxPreviewKeyDown;
            _margin.CommandLineTextBox.PreviewTextInput += OnCommandLineTextBoxPreviewTextInput;
            _margin.CommandLineTextBox.TextChanged += OnCommandLineTextBoxTextChanged;
            _margin.CommandLineTextBox.SelectionChanged += OnCommandLineTextBoxSelectionChanged;
            _margin.CommandLineTextBox.LostKeyboardFocus += OnCommandLineTextBoxLostKeyboardFocus;
            _margin.CommandLineTextBox.PreviewMouseDown += OnCommandLineTextBoxPreviewMouseDown;
            _editorFormatMap.FormatMappingChanged += OnFormatMappingChanged;
        }

        internal void Disconnect()
        {
            _vimBuffer.SwitchedMode -= OnSwitchMode;
            _vimBuffer.KeyInputStart -= OnKeyInputStart;
            _vimBuffer.KeyInputEnd -= OnKeyInputEnd;
            _vimBuffer.StatusMessage -= OnStatusMessage;
            _vimBuffer.ErrorMessage -= OnErrorMessage;
            _vimBuffer.WarningMessage -= OnWarningMessage;
            _vimBuffer.KeyInputProcessed -= OnKeyInputProcessed;
            _vimBuffer.CommandMode.CommandChanged -= OnCommandModeCommandChanged;
            _vimBuffer.TextView.GotAggregateFocus -= OnGotAggregateFocus;
            _vimBuffer.Vim.MacroRecorder.RecordingStarted -= OnRecordingStarted;
            _vimBuffer.Vim.MacroRecorder.RecordingStopped -= OnRecordingStopped;
            _margin.Loaded -= OnCommandMarginLoaded;
            _margin.Unloaded -= OnCommandMarginUnloaded;
            _margin.CommandLineTextBox.PreviewKeyDown -= OnCommandLineTextBoxPreviewKeyDown;
            _margin.CommandLineTextBox.PreviewTextInput -= OnCommandLineTextBoxPreviewTextInput;
            _margin.CommandLineTextBox.TextChanged -= OnCommandLineTextBoxTextChanged;
            _margin.CommandLineTextBox.SelectionChanged -= OnCommandLineTextBoxSelectionChanged;
            _margin.CommandLineTextBox.LostKeyboardFocus -= OnCommandLineTextBoxLostKeyboardFocus;
            _margin.CommandLineTextBox.PreviewMouseDown -= OnCommandLineTextBoxPreviewMouseDown;
            _editorFormatMap.FormatMappingChanged -= OnFormatMappingChanged;
        }

        internal void Reset()
        {
            UpdateCommandLineForNoEvent();
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

        private void UpdateForSwitchMode(IMode currentMode)
        {
            var status = CommandMarginUtil.GetStatus(_vimBuffer, currentMode, forModeSwitch: true);
            UpdateCommandLine(status);
        }

        /// <summary>
        /// Update the status in command line at the end of a key press event which didn't result in 
        /// a mode change
        /// </summary>
        private void UpdateCommandLineForNoEvent()
        {
            // In the middle of an edit the edit edit box is responsible for keeping the 
            // text up to date 
            if (_editKind != EditKind.None)
            {
                return;
            }

            var status = CommandMarginUtil.GetStatus(_vimBuffer, _vimBuffer.Mode, forModeSwitch: false);
            UpdateCommandLine(status);
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
        private void UpdateStatusLineVisibility()
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

        /// <summary>
        /// Update the color of the editor portion of the command window to be the user
        /// defined values
        /// </summary>
        private void UpdateTextColor()
        {
            if (SystemParameters.HighContrast)
            {
                var textProperties = _classificationFormatMap.DefaultTextProperties;
                _margin.TextForeground = textProperties.ForegroundBrush;
                _margin.TextBackground = textProperties.BackgroundBrush;
            }
            else
            {
                var propertyMap = _editorFormatMap.GetProperties(CommandMarginFormatDefinition.Name);
                _margin.TextForeground = propertyMap.GetForegroundBrush(SystemColors.WindowTextBrush);
                _margin.TextBackground = propertyMap.GetBackgroundBrush(SystemColors.WindowBrush);
            }
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
        /// Insert text directly into the command line at the insertion point.
        /// </summary>
        /// <param name="text"></param>
        private void InsertIntoCommandLine(string text, bool putCaretAfter)
        {
            var textBox = _margin.CommandLineTextBox;
            var builder = new StringBuilder();
            var offset = textBox.SelectionStart;
            var commandText = textBox.Text;
            builder.Append(commandText, 0, offset);
            builder.Append(text);
            builder.Append(commandText, offset, commandText.Length - offset);
            UpdateCommandLine(builder.ToString());
            if (putCaretAfter)
            {
                offset += text.Length;
            }
            textBox.Select(offset, 0);
        }

        /// <summary>
        /// This method handles keeping the history buffer in sync with the commandline display text 
        /// which do not happen otherwise for EditKind.None.
        /// </summary>
        private bool HandleHistoryNavigation(KeyInput keyInput)
        {
            var handled = _vimBuffer.Process(keyInput).IsAnyHandled;
            var prefixChar = GetPrefixChar(_editKind);
            if (handled && _editKind != EditKind.None && prefixChar.HasValue)
            {
                switch (_editKind)
                {
                    case EditKind.Command:
                        UpdateCommandLine(prefixChar.ToString() + _vimBuffer.CommandMode.Command);
                        break;

                    case EditKind.SearchForward:
                    case EditKind.SearchBackward:
                        UpdateCommandLine(prefixChar.ToString() + _vimBuffer.IncrementalSearch.CurrentSearchText);
                        break;
                }
                _margin.UpdateCaretPosition(EditPosition.End);
            }
            return handled;
        }

        /// <summary>
        /// This method handles the KeyInput as it applies to command line editor.  Make sure to 
        /// mark the key as handled if we use it here.  If we don't then it will propagate out to 
        /// the editor and be processed again
        /// </summary>
        internal void HandleKeyEvent(KeyEventArgs e)
        {
            if (InPasteWait)
            {
                HandleKeyEventInPasteWait(e);
                return;
            }
            switch (e.Key)
            {
                case Key.Escape:
                    _vimBuffer.Process(KeyInputUtil.EscapeKey);
                    ChangeEditKind(EditKind.None);
                    e.Handled = true;
                    break;
                case Key.C:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        if (_margin.CommandLineTextBox.SelectionLength != 0)
                        {
                            // Copy if there is a selection.
                            // Reported in issue #2338.
                            _clipboardDevice.Text = _margin.CommandLineTextBox.SelectedText;
                        }
                        else
                        {
                            _vimBuffer.Process(KeyInputUtil.EscapeKey);
                            ChangeEditKind(EditKind.None);
                        }
                        e.Handled = true;
                    }
                    break;
                case Key.Return:
                    ExecuteCommand(_margin.CommandLineTextBox.Text);
                    e.Handled = true;
                    break;
                case Key.J:
                case Key.M:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        ExecuteCommand(_margin.CommandLineTextBox.Text);
                        e.Handled = true;
                    }
                    break;
                case Key.Up:
                    e.Handled = HandleHistoryNavigation(KeyInputUtil.VimKeyToKeyInput(VimKey.Up));
                    break;
                case Key.Down:
                    e.Handled = HandleHistoryNavigation(KeyInputUtil.VimKeyToKeyInput(VimKey.Down));
                    break;
                case Key.Home:
                    if ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        _margin.UpdateCaretPosition(EditPosition.Start);
                        e.Handled = true;
                    }
                    break;
                case Key.End:
                    if ((e.KeyboardDevice.Modifiers & ModifierKeys.Shift) == 0)
                    {
                        _margin.UpdateCaretPosition(EditPosition.End);
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                    // Ignore left arrow if at start position
                    e.Handled = _margin.IsCaretAtStart();
                    break;
                case Key.Back:
                    // Backspacing past the beginning aborts the command/search.
                    if (_margin.CommandLineTextBox.Text.Length <= 1)
                    {
                        _vimBuffer.Process(KeyInputUtil.EscapeKey);
                        ChangeEditKind(EditKind.None);
                        e.Handled = true;
                    }
                    else if (_margin.CommandLineTextBox.CaretIndex == 1)
                    {
                        // don't let the caret get behind the initial character
                        e.Handled = true;
                    }
                    break;
                case Key.Tab:
                    InsertIntoCommandLine("\t", putCaretAfter: true);
                    var commandText = _margin.CommandLineTextBox.Text;
                    UpdateVimBufferStateWithCommandText(commandText);
                    e.Handled = true;
                    break;
                case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        // During edits we are responsible for handling the command line.  Need to 
                        // put a " into the box at the edit position
                        _pasteWaitMemo.Set(_margin.CommandLineTextBox);
                        InsertIntoCommandLine("\"", putCaretAfter: false);

                        // Now move the buffer into paste wait 
                        _vimBuffer.Process(KeyInputUtil.ApplyKeyModifiersToChar('r', VimKeyModifiers.Control));
                        e.Handled = true;
                    }
                    break;
                case Key.U:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        var textBox = _margin.CommandLineTextBox;
                        if(textBox.SelectionStart > 1)
                        {
                            var text = textBox.Text.Substring(textBox.SelectionStart);
                            textBox.Text = text;
                            
                            UpdateVimBufferStateWithCommandText(text);
                            textBox.Select(1, 0);
                        }
                        e.Handled = true;
                    }
                    break;
                case Key.W:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        DeleteWordBeforeCursor();
                        e.Handled = true;
                    }
                    break;
                case Key.P:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        e.Handled = HandleHistoryNavigation(KeyInputUtil.ApplyKeyModifiersToChar('p', VimKeyModifiers.Control));
                    }
                    break;
                case Key.N:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        e.Handled = HandleHistoryNavigation(KeyInputUtil.ApplyKeyModifiersToChar('n', VimKeyModifiers.Control));
                    }
                    break;
                case Key.D6:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        ToggleLanguage();
                        e.Handled = true;
                    }
                    break;
            }
        }
        
        /// <summary>
        /// This method handles the text composition event as it applies to command line editor.
        /// Make sure to mark the key as handled if we use it here.  If we don't then it will
        /// propagate to the the editor and be processed again
        /// </summary>
        internal void HandleCharEvent(TextCompositionEventArgs e)
        {
            if (e.ControlText.Length == 1)
            {
                var textChar = e.ControlText[0];
                switch (textChar)
                {
                    case (char)0x1B: // <C-[>
                        _vimBuffer.Process(KeyInputUtil.EscapeKey);
                        ChangeEditKind(EditKind.None);
                        e.Handled = true;
                        break;
                    case (char)0x1E: // <C-^>
                        ToggleLanguage();
                        e.Handled = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Toggle the use of typing language characters
        /// </summary>
        private void ToggleLanguage()
        {
            var isForInsert = !_vimBuffer.IncrementalSearch.HasActiveSession;
            _commonOperations.ToggleLanguage(isForInsert);
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
        internal string UpdateVimBufferStateWithCommandText(string commandText)
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
                        if (_vimBuffer.IncrementalSearch.ActiveSession.IsSome())
                        {
                            _vimBuffer.IncrementalSearch.ActiveSession.Value.ResetSearch(commandText);
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
            return commandText;
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

            // Change the edit kind after updating the command text
            // for the final time.
            command = UpdateVimBufferStateWithCommandText(command);
            ChangeEditKind(EditKind.None);

            // Process virtual key inputs for the individual
            // characters of the command to permit key input
            // listeners (like the macro recorder) to see them.
            // When processing virtual key input events, we
            // mark them as handled so they don't get processed
            // by the mode of the buffer.
            _processingVirtualKeyInputs = true;
            try
            {
                foreach (var c in command)
                {
                    _vimBuffer.Process(KeyInputUtil.CharToKeyInput(c));
                }
            }
            finally
            {
                _processingVirtualKeyInputs = false;
            }

            // Process (for real) the enter key.
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
                UpdateForSwitchMode(args.CurrentMode);
            }
        }

        private void OnKeyInputStart(object sender, KeyInputStartEventArgs args)
        {
            // If we generated a virtual key event, mark the event as handled
            // to prevent the buffer's mode from processing it.
            if (_processingVirtualKeyInputs)
            {
                args.Handled = true;
                return;
            }

            _vimBufferKeyEventState.KeyInputEventCount++;
            CheckEnableCommandLineEdit(args);
        }

        private void OnKeyInputEnd(object sender, KeyInputEventArgs args)
        {
            // Ignore our own virtual key input events.
            if (_processingVirtualKeyInputs)
            {
                return;
            }

            Debug.Assert(_vimBufferKeyEventState.InEvent);
            var updateCommandLine = true;
            try
            {
                if (!string.IsNullOrEmpty(_vimBufferKeyEventState.Message))
                {
                    UpdateCommandLine(_vimBufferKeyEventState.Message);
                    updateCommandLine = false;
                }
                else if (_vimBufferKeyEventState.SwitchModeEventArgs != null)
                {
                    var switchArgs = _vimBufferKeyEventState.SwitchModeEventArgs;
                    UpdateForSwitchMode(switchArgs.CurrentMode);
                }
                else
                {
                    UpdateCommandLineForNoEvent();
                }

                UpdateStatusLineVisibility();
            }
            finally
            {
                _vimBufferKeyEventState.KeyInputEventCount--;
                _vimBufferKeyEventState.Clear();
            }

            // On entering command mode or forward/backward search, the EditKind state is only
            // updated at KeyInputEnd (and not KeyInputStart), so we need to check again here

            var editKind = CalculateCommandLineEditKind();
            if (editKind != _editKind)
            {
                ChangeEditKind(editKind, updateCommandLine);
                if (editKind != EditKind.None)
                {
                    _margin.UpdateCaretPosition(EditPosition.End);
                }
            }
            
            UpdateShowCommandText();
        }

        private void OnKeyInputProcessed(object sender, KeyInputProcessedEventArgs e)
        {
            UpdateShowCommandText();
        }

        private void UpdateShowCommandText()
        {
            if (!_vimBuffer.GlobalSettings.ShowCommand)
            {
                _margin.ShowCommandText.Visibility = Visibility.Collapsed;
                return;
            }
            string text = CommandMarginUtil.GetShowCommandText(_vimBuffer);
            _margin.ShowCommandText.Text = text;
            _margin.ShowCommandText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
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

            UpdateCommandLineForNoEvent();
        }

        private void OnCommandLineTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            HandleKeyEvent(e);
        }

        public void OnCommandLineTextBoxPreviewTextInput(object sender, TextCompositionEventArgs args)
        {
            HandleCharEvent(args);
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
                var update = false;
                if (string.IsNullOrEmpty(command))
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
            if (kind == EditKind.Command && textBox.SelectionStart == 0 && textBox.Text.Length > 0)
            {
                textBox.SelectionStart = 1;
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

            if (_vimBuffer.IncrementalSearch.HasActiveSession)
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
                    var name = RegisterName.OfChar(c);
                    var toPaste = name.IsSome() ? _vimBuffer.GetRegister(name.Value).StringValue : string.Empty;
                    EndPasteWait(toPaste);

                    return;
                }
            }

            // The buffer was in a paste wait but the UI isn't in sync for completing
            // the operation.  Just pass Escape down to the buffer so it will cancel out
            // of paste wait and go back to a known state
            _vimBuffer.Process(KeyInputUtil.EscapeKey);
        }

        private void CancelPasteWait()
        {
            Debug.Assert(InPasteWait);
            // Cancel the paste-wait state in the buffer
            // TODO: see if there is a better way to do this
            _vimBuffer.Process(KeyInputUtil.ApplyKeyModifiersToChar(RegisterName.Blackhole.Char.Value, VimKeyModifiers.Control));
            EndPasteWait();
        }

        private void EndPasteWait(string pasteText = null)
        {
            pasteText = pasteText ?? string.Empty;

            var builder = new StringBuilder();
            var caretIndex = _pasteWaitMemo.CaretIndex;
            var commandText = _pasteWaitMemo.CommandText;
            builder.Append(commandText, 0, caretIndex);
            builder.Append(pasteText);
            builder.Append(commandText, caretIndex, commandText.Length - caretIndex);
            var command = builder.ToString();
            
            _margin.CommandLineTextBox.Text = command;
            _margin.CommandLineTextBox.Select(caretIndex + pasteText.Length, 0);
            _pasteWaitMemo.Clear();
        }

        private void DeleteWordBeforeCursor()
        {
            var textBox = _margin.CommandLineTextBox;
            var caretIndex = textBox.SelectionStart;
            if (caretIndex < 2)
                return;
            var wordSpan = TextUtil.FindPreviousWordSpan(WordKind.NormalWord, textBox.Text, caretIndex - 1);
            if (wordSpan == null)
                return;
            var text = textBox.Text[0] +
                          textBox.Text.Substring(1, Math.Max(0, wordSpan.Value.Start - 1)) +
                          textBox.Text.Substring(caretIndex);
            textBox.Text = text;
            textBox.Select(Math.Max(1, wordSpan.Value.Start), 0);
        }

        private void HandleKeyEventInPasteWait(KeyEventArgs e)
        {
            Debug.Assert(InPasteWait);
            
            switch (e.Key)
            {
               case Key.R:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        e.Handled = true; // remain in paste-wait state
                    }
                   break;
                case Key.A:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        e.Handled = HandlePasteSpecial(KeyInputUtil.ApplyKeyModifiersToChar('a', VimKeyModifiers.Control), WordKind.BigWord);
                    }
                    break;
                case Key.W:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        e.Handled = HandlePasteSpecial(KeyInputUtil.ApplyKeyModifiersToChar('w', VimKeyModifiers.Control), WordKind.NormalWord);
                    }
                    break;
                case Key.J:
                case Key.M:
                    if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
                    {
                        CancelPasteWait();
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                case Key.Enter:
                case Key.Left:
                case Key.Right:
                case Key.Back:
                case Key.Delete:
                    CancelPasteWait();
                    e.Handled = true;
                    break;
            }
        }

        private bool HandlePasteSpecial(KeyInput keyInput, WordKind wordKind)
        {
            Debug.Assert(InPasteWait);
            
            if(!_vimBuffer.Process(keyInput).IsAnyHandled)
                return false;

            var currentWord = _vimBuffer.MotionUtil.GetMotion(Motion.NewInnerWord(wordKind), new MotionArgument(MotionContext.AfterOperator));
            string pasteText = currentWord.IsSome() ? currentWord.Value.Span.GetText(): string.Empty;
            
            EndPasteWait(pasteText);
            return true;
        }

        private class PasteWaitMemo
        {
            public int CaretIndex { get; private set; }
            public string CommandText { get; private set; } = string.Empty;

            public void Clear()
            {
                Set(0, string.Empty);
            }

            public void Set(TextBox textBox)
            {
                Set(textBox.CaretIndex, textBox.Text);
            }

            private void Set(int caretIndex, string commandText)
            {
                CaretIndex = caretIndex;
                CommandText = commandText;
            }
        }
    }
}
