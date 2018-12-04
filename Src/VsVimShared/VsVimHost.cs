﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.OLE.Interop;
using EnvDTE80;
using System.Windows.Threading;
using System.Diagnostics;

namespace Vim.VisualStudio
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(VsVimHost))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VsVimHost : VimHost, IVsSelectionEvents, IVsRunningDocTableEvents3
    {
        #region SettingsSource

        /// <summary>
        /// This class provides the ability to control our host specific settings using the familiar
        /// :set syntax in a vim file.  It is just proxying them to the real IVimApplicationSettings
        /// </summary>
        internal sealed class SettingsSource : IVimCustomSettingSource
        {
            private const string UseEditorIndentName = "vsvim_useeditorindent";
            private const string UseEditorDefaultsName = "vsvim_useeditordefaults";
            private const string UseEditorTabAndBackspaceName = "vsvim_useeditortab";
            private const string UseEditorCommandMarginName = "vsvim_useeditorcommandmargin";
            private const string CleanMacrosName = "vsvim_cleanmacros";
            private const string HideMarksName = "vsvim_hidemarks";

            private readonly IVimApplicationSettings _vimApplicationSettings;

            private SettingsSource(IVimApplicationSettings vimApplicationSettings)
            {
                _vimApplicationSettings = vimApplicationSettings;
            }

            internal static void Initialize(IVimGlobalSettings globalSettings, IVimApplicationSettings vimApplicationSettings)
            {
                var settingsSource = new SettingsSource(vimApplicationSettings);
                globalSettings.AddCustomSetting(UseEditorIndentName, UseEditorIndentName, settingsSource);
                globalSettings.AddCustomSetting(UseEditorDefaultsName, UseEditorDefaultsName, settingsSource);
                globalSettings.AddCustomSetting(UseEditorTabAndBackspaceName, UseEditorTabAndBackspaceName, settingsSource);
                globalSettings.AddCustomSetting(UseEditorCommandMarginName, UseEditorCommandMarginName, settingsSource);
                globalSettings.AddCustomSetting(CleanMacrosName, CleanMacrosName, settingsSource);
                globalSettings.AddCustomSetting(HideMarksName, HideMarksName, settingsSource);
            }

            SettingValue IVimCustomSettingSource.GetDefaultSettingValue(string name)
            {
                switch (name)
                {
                    case UseEditorIndentName:
                    case UseEditorDefaultsName:
                    case UseEditorTabAndBackspaceName:
                    case UseEditorCommandMarginName:
                    case CleanMacrosName:
                        return SettingValue.NewToggle(false);
                    case HideMarksName:
                        return SettingValue.NewString("");
                    default:
                        Debug.Assert(false);
                        return SettingValue.NewToggle(false);
               }
            }

            SettingValue IVimCustomSettingSource.GetSettingValue(string name)
            {
                switch (name)
                {
                    case UseEditorIndentName:
                        return SettingValue.NewToggle(_vimApplicationSettings.UseEditorIndent);
                    case UseEditorDefaultsName:
                        return SettingValue.NewToggle(_vimApplicationSettings.UseEditorDefaults);
                    case UseEditorTabAndBackspaceName:
                        return SettingValue.NewToggle(_vimApplicationSettings.UseEditorTabAndBackspace);
                    case UseEditorCommandMarginName:
                        return SettingValue.NewToggle(_vimApplicationSettings.UseEditorCommandMargin);
                    case CleanMacrosName:
                        return SettingValue.NewToggle(_vimApplicationSettings.CleanMacros);
                    case HideMarksName:
                        return SettingValue.NewString(_vimApplicationSettings.HideMarks);
                    default:
                        Debug.Assert(false);
                        return SettingValue.NewToggle(false);
                }
            }

            void IVimCustomSettingSource.SetSettingValue(string name, SettingValue settingValue)
            {
                void setBool(Action<bool> action)
                {
                    if (!settingValue.IsToggle)
                    {
                        return;
                    }

                    var value = ((SettingValue.Toggle)settingValue).Toggle;
                    action(value);
                }

                void setString(Action<string> action)
                {
                    if (!settingValue.IsString)
                    {
                        return;
                    }

                    var value = ((SettingValue.String)settingValue).String;
                    action(value);
                }

                switch (name)
                {
                    case UseEditorIndentName:
                        setBool(v => _vimApplicationSettings.UseEditorIndent = v);
                        break;
                    case UseEditorDefaultsName:
                        setBool(v => _vimApplicationSettings.UseEditorDefaults = v);
                        break;
                    case UseEditorTabAndBackspaceName:
                        setBool(v => _vimApplicationSettings.UseEditorTabAndBackspace = v);
                        break;
                    case UseEditorCommandMarginName:
                        setBool(v => _vimApplicationSettings.UseEditorCommandMargin = v);
                        break;
                    case CleanMacrosName:
                        setBool(v => _vimApplicationSettings.CleanMacros = v);
                        break;
                    case HideMarksName:
                        setString(v => _vimApplicationSettings.HideMarks = v);
                        break;
                    default:
                        Debug.Assert(false);
                        break;
                }
            }
        }

        #endregion

        #region SettingsSync

        internal sealed class SettingsSync
        {
            private bool _isSyncing;

            public IVimApplicationSettings VimApplicationSettings { get; }
            public IMarkDisplayUtil MarkDisplayUtil { get; }
            public IControlCharUtil ControlCharUtil { get; }

            [ImportingConstructor]
            public SettingsSync(IVimApplicationSettings vimApplicationSettings, IMarkDisplayUtil markDisplayUtil, IControlCharUtil controlCharUtil)
            {
                VimApplicationSettings = vimApplicationSettings;
                MarkDisplayUtil = markDisplayUtil;
                ControlCharUtil = controlCharUtil;

                MarkDisplayUtil.HideMarksChanged += SyncToApplicationSettings;
                ControlCharUtil.DisplayControlCharsChanged += SyncToApplicationSettings;
                VimApplicationSettings.SettingsChanged += SyncFromApplicationSettings;
            }

            /// <summary>
            /// Sync from our external sources to application settings
            /// </summary>
            internal void SyncToApplicationSettings(object sender = null, EventArgs e = null)
            {
                SyncAction(() =>
                {
                    VimApplicationSettings.HideMarks = MarkDisplayUtil.HideMarks;
                    VimApplicationSettings.DisplayControlChars = ControlCharUtil.DisplayControlChars;
                });
            }

            internal void SyncFromApplicationSettings(object sender = null, EventArgs e = null)
            {
                SyncAction(() =>
                {
                    MarkDisplayUtil.HideMarks = VimApplicationSettings.HideMarks;
                    ControlCharUtil.DisplayControlChars = VimApplicationSettings.DisplayControlChars;
                });
           }

            private void SyncAction(Action action)
            {
                if (!_isSyncing)
                {
                    try
                    {
                        _isSyncing = true;
                        action();
                    }
                    finally
                    {
                        _isSyncing = false;
                    }
                }
            }
        }

        #endregion

        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly IVsAdapter _vsAdapter;
        private readonly ITextManager _textManager;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly _DTE _dte;
        private readonly IVsExtensibility _vsExtensibility;
        private readonly ISharedService _sharedService;
        private readonly IVsMonitorSelection _vsMonitorSelection;
        private readonly IVimApplicationSettings _vimApplicationSettings;
        private readonly ISmartIndentationService _smartIndentationService;
        private readonly IExtensionAdapterBroker _extensionAdapterBroker;
        private readonly IVsRunningDocumentTable _runningDocumentTable;
        private readonly IVsShell _vsShell;
        private readonly IProtectedOperations _protectedOperations;
        private readonly SettingsSync _settingsSync;
        private IVim _vim;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        /// <summary>
        /// Should we create IVimBuffer instances for new ITextView values
        /// </summary>
        public bool DisableVimBufferCreation
        {
            get;
            set;
        }

        /// <summary>
        /// Don't automatically synchronize settings.  The settings can't be synchronized until after Visual Studio 
        /// applies settings which happens at an uncertain time.  HostFactory handles this timing 
        /// </summary>
        public override bool AutoSynchronizeSettings
        {
            get { return false; }
        }

        public override DefaultSettings DefaultSettings
        {
            get { return _vimApplicationSettings.DefaultSettings; }
        }

        public override bool IsUndoRedoExpected
        {
            get { return _extensionAdapterBroker.IsUndoRedoExpected ?? base.IsUndoRedoExpected; }
        }

        public override int TabCount
        {
            get { return _sharedService.GetWindowFrameState().WindowFrameCount; }
        }

        public override bool UseDefaultCaret
        {
            get { return _extensionAdapterBroker.UseDefaultCaret ?? base.UseDefaultCaret; }
        }

        [ImportingConstructor]
        internal VsVimHost(
            IVsAdapter adapter,
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            ITextBufferUndoManagerProvider undoManagerProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService,
            ISmartIndentationService smartIndentationService,
            ITextManager textManager,
            ISharedServiceFactory sharedServiceFactory,
            IVimApplicationSettings vimApplicationSettings,
            IExtensionAdapterBroker extensionAdapterBroker,
            IProtectedOperations protectedOperations,
            IMarkDisplayUtil markDisplayUtil,
            IControlCharUtil controlCharUtil,
            SVsServiceProvider serviceProvider)
            : base(textBufferFactoryService, textEditorFactoryService, textDocumentFactoryService, editorOperationsFactoryService)
        {
            _vsAdapter = adapter;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _vsExtensibility = (IVsExtensibility)serviceProvider.GetService(typeof(IVsExtensibility));
            _textManager = textManager;
            _sharedService = sharedServiceFactory.Create();
            _vsMonitorSelection = serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();
            _vimApplicationSettings = vimApplicationSettings;
            _smartIndentationService = smartIndentationService;
            _extensionAdapterBroker = extensionAdapterBroker;
            _runningDocumentTable = serviceProvider.GetService<SVsRunningDocumentTable, IVsRunningDocumentTable>();
            _vsShell = (IVsShell)serviceProvider.GetService(typeof(SVsShell));
            _protectedOperations = protectedOperations;

            _vsMonitorSelection.AdviseSelectionEvents(this, out uint selectionCookie);
            _runningDocumentTable.AdviseRunningDocTableEvents(this, out uint runningDocumentTableCookie);

            InitOutputPane();

            _settingsSync = new SettingsSync(vimApplicationSettings, markDisplayUtil, controlCharUtil);
            _settingsSync.SyncFromApplicationSettings();
        }

        /// <summary>
        /// Hookup the output window to the vim trace data when it's requested by the developer
        /// </summary>
        private void InitOutputPane()
        {
            // The output window is not guaraneed to be accessible on startup. On certain configurations of VS2015
            // it can throw an exception. Delaying the creation of the Window until after startup has likely 
            // completed. Additionally using IProtectedOperations to guard against exeptions 
            // https://github.com/jaredpar/VsVim/issues/2249

            _protectedOperations.BeginInvoke(initOutputPaneCore, DispatcherPriority.ApplicationIdle);

            void initOutputPaneCore()
            {
                if (!(_dte is DTE2 dte2))
                {
                    return;
                }

                var outputWindow = dte2.ToolWindows.OutputWindow;
                var outputPane = outputWindow.OutputWindowPanes.Add("VsVim");

                VimTrace.Trace += (_, e) =>
                {
                    if (_vimApplicationSettings.EnableOutputWindow)
                    {
                        outputPane.OutputString(e.Message + Environment.NewLine);
                    }
                };
            } 
        }

        public override void EnsurePackageLoaded()
        {
            var guid = VsVimConstants.PackageGuid;
            _vsShell.LoadPackage(ref guid, out IVsPackage package);
        }

        public override void CloseAllOtherTabs(ITextView textView)
        {
            RunHostCommand(textView, "File.CloseAllButThis", string.Empty);
        }

        public override void CloseAllOtherWindows(ITextView textView)
        {
            CloseAllOtherTabs(textView); // At least for now, :only == :tabonly
        }

        private bool SafeExecuteCommand(ITextView contextTextView, string command, string args = "")
        {
            try
            {
                // Many Visual Studio commands expect focus to be in the editor when 
                // running.  Switch focus there if an appropriate ITextView is available
                if (contextTextView is IWpfTextView wpfTextView)
                {
                    wpfTextView.VisualElement.Focus();
                }

                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>  
        /// Get the C++ identifier which exists under the caret 
        /// </summary>
        private static string GetCPlusPlusIdentifier(ITextView textView)
        {
            var snapshot = textView.TextSnapshot;
            bool isValid(int position)
            {
                if (position < 0 || position >= snapshot.Length)
                {
                    return false;
                }

                var c = snapshot[position];
                return char.IsLetter(c) || char.IsDigit(c) || c == '_';
            }

            var start = textView.Caret.Position.BufferPosition.Position;
            if (!isValid(start))
            {
                return null;
            }

            var end = start + 1;
            while (isValid(end))
            {
                end++;
            }

            while (isValid(start - 1))
            {
                start--;
            }

            var span = new SnapshotSpan(snapshot, start, end - start);
            return span.GetText();
        }

        /// <summary>
        /// The C++ project system requires that the target of GoToDefinition be passed
        /// as an argument to the command.  
        /// </summary>
        private bool GoToDefinitionCPlusPlus(ITextView textView, string target)
        {
            if (target == null)
            {
                target = GetCPlusPlusIdentifier(textView);
            }

            if (target != null)
            {
                return SafeExecuteCommand(textView, CommandNameGoToDefinition, target);
            }

            return SafeExecuteCommand(textView, CommandNameGoToDefinition);
        }

        private bool GoToDefinitionCore(ITextView textView, string target)
        {
            if (textView.TextBuffer.ContentType.IsCPlusPlus())
            {
                return GoToDefinitionCPlusPlus(textView, target);
            }
            else
            {
                return SafeExecuteCommand(textView, CommandNameGoToDefinition);
            }
        }

        /// <summary>
        /// Treat a bulk operation just like a macro replay.  They have similar semantics like we
        /// don't want intellisense to be displayed during the operation.  
        /// </summary>
        public override void BeginBulkOperation()
        {
            try
            {
                _vsExtensibility.EnterAutomationFunction();
            }
            catch
            {
                // If automation support isn't present it's not an issue
            }
        }

        public override void EndBulkOperation()
        {
            try
            {
                _vsExtensibility.ExitAutomationFunction();
            }
            catch
            {
                // If automation support isn't present it's not an issue
            }
        }

        /// <summary>
        /// Format the specified line range.  There is no inherent operation to do this
        /// in Visual Studio.  Instead we leverage the FormatSelection command.  Need to be careful
        /// to reset the selection after a format
        /// </summary>
        public override void FormatLines(ITextView textView, SnapshotLineRange range)
        {
            var startedWithSelection = !textView.Selection.IsEmpty;
            textView.Selection.Clear();
            textView.Selection.Select(range.ExtentIncludingLineBreak, false);
            SafeExecuteCommand(textView, "Edit.FormatSelection");

            if (!startedWithSelection)
            {
                textView.Selection.Clear();
            }
        }

        public override bool GoToDefinition()
        {
            return GoToDefinitionCore(_textManager.ActiveTextViewOptional, null);
        }

        /// <summary>
        /// In a perfect world this would replace the contents of the existing ITextView
        /// with those of the specified file.  Unfortunately this causes problems in 
        /// Visual Studio when the file is of a different content type.  Instead we 
        /// mimic the behavior by opening the document in a new window and closing the
        /// existing one
        /// </summary>
        public override bool LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            try
            {
                // Open the document before closing the other.  That way any error which occurs
                // during an open will cause the method to abandon and produce a user error 
                // message
                VsShellUtilities.OpenDocument(_vsAdapter.ServiceProvider, filePath);
                _textManager.CloseView(textView);
                return true;
            }
            catch (Exception e)
            {
                _vim.ActiveStatusUtil.OnError(e.Message);
                return false;
            }
        }

        /// <summary>
        /// Open up a new document window with the specified file
        /// </summary>
        public override bool LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
        {
            try
            {
                // Open the document in a window.
                VsShellUtilities.OpenDocument(_vsAdapter.ServiceProvider, filePath, VSConstants.LOGVIEWID_Primary,
                    out IVsUIHierarchy hierarchy, out uint itemID, out IVsWindowFrame windowFrame);

                if (line.IsSome())
                {
                    // Get the VS text view for the window.
                    var vsTextView = VsShellUtilities.GetTextView(windowFrame);

                    // Get the WPF text view for the VS text view.
                    var wpfTextView = _editorAdaptersFactoryService.GetWpfTextView(vsTextView);

                    // Move the caret to its initial position.
                    var snapshotLine = wpfTextView.TextSnapshot.GetLineFromLineNumber(line.Value);
                    var point = snapshotLine.Start;
                    if (column.IsSome())
                    {
                        point = point.Add(column.Value);
                        wpfTextView.Caret.MoveTo(point);
                    }
                    else
                    {
                        // Default column implies moving to the first non-blank.
                        wpfTextView.Caret.MoveTo(point);
                        var editorOperations = EditorOperationsFactoryService.GetEditorOperations(wpfTextView);
                        editorOperations.MoveToStartOfLineAfterWhiteSpace(false);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                _vim.ActiveStatusUtil.OnError(e.Message);
                return false;
            }
        }

        public override bool NavigateTo(VirtualSnapshotPoint point)
        {
            return _textManager.NavigateTo(point);
        }

        public override string GetName(ITextBuffer buffer)
        {
            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(buffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return string.Empty;
            }
            return vsTextLines.GetFileName();
        }

        public override bool Save(ITextBuffer textBuffer)
        {
            // The best way to save a buffer from an extensbility stand point is to use the DTE command 
            // system.  This means save goes through IOleCommandTarget and hits the maximum number of 
            // places other extension could be listening for save events.
            //
            // This only works though when we are saving the buffer which currently has focus.  If it's 
            // not in focus then we need to resort to saving via the ITextDocument.
            var activeSave = SaveActiveTextView(textBuffer);
            if (activeSave != null)
            {
                return activeSave.Value;
            }

            return _textManager.Save(textBuffer).IsSuccess;
        }

        /// <summary>
        /// Do a save operation using the <see cref="IOleCommandTarget"/> approach if this is a buffer
        /// for the active text view.  Returns null when this operation couldn't be performed and a 
        /// non-null value when the operation was actually executed.
        /// </summary>
        private bool? SaveActiveTextView(ITextBuffer textBuffer)
        {
            IWpfTextView activeTextView;
            if (!_vsAdapter.TryGetActiveTextView(out activeTextView) ||
                !TextBufferUtil.GetSourceBuffersRecursive(activeTextView.TextBuffer).Contains(textBuffer))
            {
                return null;
            }

            return SafeExecuteCommand(activeTextView, "File.SaveSelectedItems");
        }

        public override bool SaveTextAs(string text, string fileName)
        {
            try
            {
                File.WriteAllText(fileName, text);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override void Close(ITextView textView)
        {
            _textManager.CloseView(textView);
        }

        public override bool IsReadOnly(ITextBuffer textBuffer)
        {
            return _vsAdapter.IsReadOnly(textBuffer);
        }

        public override bool IsVisible(ITextView textView)
        {
            if (textView is IWpfTextView wpfTextView)
            {
                if (!wpfTextView.VisualElement.IsVisible)
                {
                    return false;
                }

                // Certain types of windows (e.g. aspx documents) always report
                // that they are visible. Use the "is on screen" predicate of
                // the window's frame to rule them out. Reported in issue
                // #2435.
                var frameResult = _vsAdapter.GetContainingWindowFrame(wpfTextView);
                if (frameResult.TryGetValue(out IVsWindowFrame frame))
                {
                    if (frame.IsOnScreen(out int isOnScreen) == VSConstants.S_OK)
                    {
                        if (isOnScreen == 0)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Custom process the insert command if possible.  This is handled by VsCommandTarget
        /// </summary>
        public override bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            if (VsCommandTarget.TryGet(textView, out VsCommandTarget vsCommandTarget))
            {
                return vsCommandTarget.TryCustomProcess(command);
            }

            return false;
        }

        public override int GetTabIndex(ITextView textView)
        {
            // TODO: Should look for the actual index instead of assuming this is called on the 
            // active ITextView.  They may not actually be equal
            var windowFrameState = _sharedService.GetWindowFrameState();
            return windowFrameState.ActiveWindowFrameIndex;
        }

        public override void GoToTab(int index)
        {
            _sharedService.GoToTab(index);
        }

        public override void OpenQuickFixWindow()
        {
            SafeExecuteCommand(null, "View.ErrorList");
        }

        public override bool GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            // This implementation could be much more riguorous but for next a simple navigation
            // of the next and previous error will suffice
            var command = quickFix.IsNext
                ? "View.NextError"
                : "View.PreviousError";
            for (var i = 0; i < count; i++)
            {
                SafeExecuteCommand(null, command);
            }

            return true;
        }

        public override void Make(bool jumpToFirstError, string arguments)
        {
            SafeExecuteCommand(null, "Build.BuildSolution");
        }

        public override bool TryGetFocusedTextView(out ITextView textView)
        {
            var result = _vsAdapter.GetWindowFrames();
            if (result.IsError)
            {
                textView = null;
                return false;
            }

            var activeWindowFrame = result.Value.FirstOrDefault(_sharedService.IsActiveWindowFrame);
            if (activeWindowFrame == null)
            {
                textView = null;
                return false;
            }

            // TODO: Should try and pick the ITextView which is actually focussed as 
            // there could be several in a split screen
            try
            {
                textView = activeWindowFrame.GetCodeWindow().Value.GetPrimaryTextView(_editorAdaptersFactoryService).Value;
                return textView != null;
            }
            catch
            {
                textView = null;
                return false;
            }
        }

        public override void Quit()
        {
            _dte.Quit();
        }

        public override void RunHostCommand(ITextView textView, string command, string argument)
        {
            SafeExecuteCommand(textView, command, argument);
        }

        /// <summary>
        /// Perform a horizontal window split 
        /// </summary>
        public override void SplitViewHorizontally(ITextView textView)
        {
            _textManager.SplitView(textView);
        }

        /// <summary>
        /// Perform a vertical buffer split, which is essentially just another window in a different tab group.
        /// </summary>
        public override void SplitViewVertically(ITextView value)
        {
            try
            {
                _dte.ExecuteCommand("Window.NewWindow");
                _dte.ExecuteCommand("Window.NewVerticalTabGroup");
            }
            catch (Exception e)
            {
                _vim.ActiveStatusUtil.OnError(e.Message);
            }
        }

        /// <summary>
        /// Get the point at the middle of the caret in screen coordinates
        /// </summary>
        /// <param name="textView"></param>
        /// <returns></returns>
        private Point GetScreenPoint(IWpfTextView textView)
        {
            var element = textView.VisualElement;
            var caret = textView.Caret;
            var caretX = (caret.Left + caret.Right) / 2 - textView.ViewportLeft;
            var caretY = (caret.Top + caret.Bottom) / 2 - textView.ViewportTop;
            return element.PointToScreen(new Point(caretX, caretY));
        }

        /// <summary>
        /// Get the rectangle of the window in screen coordinates including any associated
        /// elements like margins, scroll bars, etc.
        /// </summary>
        /// <param name="textView"></param>
        /// <returns></returns>
        private Rect GetScreenRect(IWpfTextView textView)
        {
            var element = textView.VisualElement;
            var parent = VisualTreeHelper.GetParent(element);
            if (parent is FrameworkElement parentElement)
            {
                // The parent is a grid that contains the text view and all its margins.
                // Unfortunately, this does not include the navigation bar, so a horizontal
                // line from the caret in a window without one might intersect the navigation
                // bar and then we would not consider it as a candidate for horizontal motion.
                // The user can work around this by moving the caret down a couple of lines.
                element = parentElement;
            }
            var size = element.RenderSize;
            var upperLeft = new Point(0, 0);
            var lowerRight = new Point(size.Width, size.Height);
            return new Rect(element.PointToScreen(upperLeft), element.PointToScreen(lowerRight));
        }

        private IEnumerable<Tuple<IWpfTextView, Rect>> GetWindowPairs()
        {
            // Build a list of all visible windows and their screen coordinates.
            return _vim.VimBuffers
                .Select(vimBuffer => vimBuffer.TextView as IWpfTextView)
                .Where(textView => textView != null)
                .Where(textView => IsVisible(textView))
                .Where(textView => textView.ViewportWidth != 0)
                .Select(textView =>
                    Tuple.Create(textView, GetScreenRect(textView)));
        }

        private bool GoToWindowVertically(IWpfTextView currentTextView, int delta)
        {
            // Find those windows that overlap a vertical line
            // passing through the caret of the current window,
            // sorted by increasing vertical position on the screen.
            var caretPoint = GetScreenPoint(currentTextView);
            var pairs = GetWindowPairs()
                .Where(pair => pair.Item2.Left <= caretPoint.X && caretPoint.X <= pair.Item2.Right)
                .OrderBy(pair => pair.Item2.Y);

            return GoToWindowCore(currentTextView, delta, false, pairs);
        }

        private bool GoToWindowHorizontally(IWpfTextView currentTextView, int delta)
        {
            // Find those windows that overlap a horizontal line
            // passing through the caret of the current window,
            // sorted by increasing horizontal position on the screen.
            var caretPoint = GetScreenPoint(currentTextView);
            var pairs = GetWindowPairs()
                .Where(pair => pair.Item2.Top <= caretPoint.Y && caretPoint.Y <= pair.Item2.Bottom)
                .OrderBy(pair => pair.Item2.X);

            return GoToWindowCore(currentTextView, delta, false, pairs);
        }

        private bool GoToWindowNext(IWpfTextView currentTextView, int delta, bool wrap)
        {
            // Sort the windows into row/column order.
            var pairs = GetWindowPairs()
                .OrderBy(pair => pair.Item2.X)
                .ThenBy(pair => pair.Item2.Y);

            return GoToWindowCore(currentTextView, delta, wrap, pairs);
        }

        private bool GoToWindowRecent(IWpfTextView currentTextView)
        {
            // Get the list of visible windows.
            var windows = GetWindowPairs().Select(pair => pair.Item1).ToList();

            // Find a recent buffer that is visible.
            var i = 1;
            while (TryGetRecentWindow(i, out IWpfTextView textView))
            {
                if (windows.Contains(textView))
                {
                    textView.VisualElement.Focus();
                    return true;
                }
                ++i;
            }

            return false;
        }

        private bool TryGetRecentWindow(int n, out IWpfTextView textView)
        {
            textView = null;
            var vimBufferOption = _vim.TryGetRecentBuffer(n);
            if (vimBufferOption.IsSome() && vimBufferOption.Value.TextView is IWpfTextView wpfTextView)
            {
                textView = wpfTextView;
            }
            return false;
        }

        public bool GoToWindowCore(IWpfTextView currentTextView, int delta, bool wrap,
            IEnumerable<Tuple<IWpfTextView, Rect>> rawPairs)
        {
            var pairs = rawPairs.ToList();

            // Find the position of the current window in that list.
            var currentIndex = pairs.FindIndex(pair => pair.Item1 == currentTextView);
            if (currentIndex == -1)
            {
                return false;
            }

            var newIndex = currentIndex + delta;
            if (wrap)
            {
                // Wrap around to a valid index.
                newIndex = (newIndex % pairs.Count + pairs.Count) % pairs.Count;
            }
            else
            {
                // Move as far as possible in the specified direction.
                newIndex = Math.Max(0, newIndex);
                newIndex = Math.Min(newIndex, pairs.Count - 1);
            }

            // Go to the resulting window.
            pairs[newIndex].Item1.VisualElement.Focus();
            return true;
        }

        public override void GoToWindow(ITextView textView, WindowKind windowKind, int count)
        {
            const int maxCount = 1000;
            var currentTextView = textView as IWpfTextView;
            if (currentTextView == null)
            {
                return;
            }

            bool result;
            switch (windowKind)
            {
                case WindowKind.Up:
                    result = GoToWindowVertically(currentTextView, -count);
                    break;
                case WindowKind.Down:
                    result = GoToWindowVertically(currentTextView, count);
                    break;
                case WindowKind.Left:
                    result = GoToWindowHorizontally(currentTextView, -count);
                    break;
                case WindowKind.Right:
                    result = GoToWindowHorizontally(currentTextView, count);
                    break;

                case WindowKind.FarUp:
                    result = GoToWindowVertically(currentTextView, -maxCount);
                    break;
                case WindowKind.FarDown:
                    result = GoToWindowVertically(currentTextView, maxCount);
                    break;
                case WindowKind.FarLeft:
                    result = GoToWindowHorizontally(currentTextView, -maxCount);
                    break;
                case WindowKind.FarRight:
                    result = GoToWindowHorizontally(currentTextView, maxCount);
                    break;

                case WindowKind.Previous:
                    result = GoToWindowNext(currentTextView, -count, true);
                    break;
                case WindowKind.Next:
                    result = GoToWindowNext(currentTextView, count, true);
                    break;

                case WindowKind.Top:
                    result = GoToWindowNext(currentTextView, -maxCount, false);
                    break;
                case WindowKind.Bottom:
                    result = GoToWindowNext(currentTextView, maxCount, false);
                    break;

                case WindowKind.Recent:
                    result = GoToWindowRecent(currentTextView);
                    break;

                default:
                    throw Contract.GetInvalidEnumException(windowKind);
            }

            if (!result)
            {
                _vim.ActiveStatusUtil.OnError("Can't move focus");
            }
        }

        public override WordWrapStyles GetWordWrapStyle(ITextView textView)
        {
            var style = WordWrapStyles.WordWrap;
            switch (_vimApplicationSettings.WordWrapDisplay)
            {
                case WordWrapDisplay.All:
                    style |= (WordWrapStyles.AutoIndent | WordWrapStyles.VisibleGlyphs);
                    break;
                case WordWrapDisplay.Glyph:
                    style |= WordWrapStyles.VisibleGlyphs;
                    break;
                case WordWrapDisplay.AutoIndent:
                    style |= WordWrapStyles.AutoIndent;
                    break;
                default:
                    Contract.Assert(false);
                    break;
            }

            return style;
        }

        public override FSharpOption<int> GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            if (_vimApplicationSettings.UseEditorIndent)
            {
                var indent = _smartIndentationService.GetDesiredIndentation(textView, newLine);
                if (indent.HasValue)
                {
                    return FSharpOption.Create(indent.Value);
                }
                else
                {
                    // If the user wanted editor indentation but the editor doesn't support indentation
                    // even though it proffers an indentation service then fall back to what auto
                    // indent would do if it were enabled (don't care if it actually is)
                    //
                    // Several editors like XAML offer the indentation service but don't actually 
                    // provide information.  User clearly wants indent there since the editor indent
                    // is enabled.  Do a best effort and us Vim style indenting
                    return FSharpOption.Create(EditUtil.GetAutoIndent(contextLine, localSettings.TabStop));
                }
            }

            return FSharpOption<int>.None;
        }

        public override bool GoToGlobalDeclaration(ITextView textView, string target)
        {
            return GoToDefinitionCore(textView, target);
        }

        public override bool GoToLocalDeclaration(ITextView textView, string target)
        {
            // This is technically incorrect as it should prefer local declarations. However 
            // there is currently no better way in Visual Studio.  Added this method though
            // so it's easier to plug in later should such an API become available
            return GoToDefinitionCore(textView, target);
        }

        public override void VimCreated(IVim vim)
        {
            _vim = vim;
            SettingsSource.Initialize(vim.GlobalSettings, _vimApplicationSettings);
        }

        public override void VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            if (vimRcState.IsLoadFailed)
            {
                // If we failed to load a vimrc file then we should add a couple of sanity 
                // settings.  Otherwise the Visual Studio experience wont't be what users expect
                localSettings.AutoIndent = true;
            }
        }

        public override bool ShouldCreateVimBuffer(ITextView textView)
        {
            if (textView.IsPeekView())
            {
                return true;
            }

            if (_vsAdapter.IsWatchWindowView(textView))
            {
                return false;
            }

            if (!_vsAdapter.IsTextEditorView(textView))
            {
                return false;
            }

            var result = _extensionAdapterBroker.ShouldCreateVimBuffer(textView);
            if (result.HasValue)
            {
                return result.Value;
            }

            if (!base.ShouldCreateVimBuffer(textView))
            {
                return false;
            }

            return !DisableVimBufferCreation;
        }

        public override bool ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            switch (_vimApplicationSettings.VimRcLoadSetting)
            {
                case VimRcLoadSetting.None:
                    return false;
                case VimRcLoadSetting.VimRc:
                    return vimRcPath.VimRcKind == VimRcKind.VimRc;
                case VimRcLoadSetting.VsVimRc:
                    return vimRcPath.VimRcKind == VimRcKind.VsVimRc;
                case VimRcLoadSetting.Both:
                    return true;
                default:
                    Contract.Assert(false);
                    return base.ShouldIncludeRcFile(vimRcPath);
            }
        }

        public override bool ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            if (_extensionAdapterBroker.ShouldKeepSelectionAfterHostCommand(command, argument) ?? false)
            {
                return true;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;
            if (comparer.Equals(command, "Edit.SurroundWith"))
            {
                // Need to keep the selection here so the surround with command knows the selection
                // to surround.
                return true;
            }

            return base.ShouldKeepSelectionAfterHostCommand(command, argument);
        }

        #region IVsSelectionEvents

        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            var id = (VSConstants.VSSELELEMID)elementid;
            if (id == VSConstants.VSSELELEMID.SEID_WindowFrame)
            {
                ITextView getTextView(object obj)
                {
                    var vsWindowFrame = obj as IVsWindowFrame;
                    if (vsWindowFrame == null)
                    {
                        return null;
                    }

                    var vsCodeWindow = vsWindowFrame.GetCodeWindow();
                    if (vsCodeWindow.IsError)
                    {
                        return null;
                    }

                    var lastActiveTextView = vsCodeWindow.Value.GetLastActiveView(_vsAdapter.EditorAdapter);
                    if (lastActiveTextView.IsError)
                    {
                        return null;
                    }

                    return lastActiveTextView.Value;
                }

                ITextView oldView = getTextView(varValueOld);
                ITextView newView = null;
                if (ErrorHandler.Succeeded(_vsMonitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out object value)))
                {
                    newView = getTextView(value);
                }

                RaiseActiveTextViewChanged(
                    oldView == null ? FSharpOption<ITextView>.None : FSharpOption.Create<ITextView>(oldView),
                    newView == null ? FSharpOption<ITextView>.None : FSharpOption.Create<ITextView>(newView));
            }

            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterSave(uint docCookie)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeSave(uint docCookie)
        {
            if (_vsAdapter.GetTextBufferForDocCookie(docCookie).TryGetValue(out ITextBuffer buffer))
            {
                RaiseBeforeSave(buffer);
            }
            return VSConstants.S_OK;
        }

        public override bool HasMultipleCarets(ITextView textView)
        {
            return _sharedService.HasMultipleCarets(textView);
        }

        #endregion
    }
}
