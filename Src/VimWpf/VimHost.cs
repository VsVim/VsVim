using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Vim.Extensions;
using Vim.Interpreter;
using System.Windows.Threading;

namespace Vim.UI.Wpf
{
    public abstract class VimHost : IVimHost, IWpfTextViewCreationListener
    {
        private readonly IProtectedOperations _protectedOperations;
        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly ITextEditorFactoryService _textEditorFactoryService;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly IEditorOperationsFactoryService _editorOperationsFactoryService;
        private readonly List<ITextView> _textViewList = new List<ITextView>();
        private event EventHandler<TextViewEventArgs> _isVisibleChanged;
        private event EventHandler<TextViewChangedEventArgs> _activeTextViewChanged;
        private event EventHandler<BeforeSaveEventArgs> _beforeSave;

        public ITextDocumentFactoryService TextDocumentFactoryService
        {
            get { return _textDocumentFactoryService; }
        }

        public ITextBufferFactoryService TextBufferFactoryService
        {
            get { return _textBufferFactoryService; }
        }

        public ITextEditorFactoryService TextEditorFactoryService
        {
            get { return _textEditorFactoryService; }
        }

        public IEditorOperationsFactoryService EditorOperationsFactoryService
        {
            get { return _editorOperationsFactoryService; }
        }

        public virtual bool AutoSynchronizeSettings
        {
            get { return true; }
        }

        public abstract int TabCount
        {
            get;
        }

        public abstract string HostIdentifier
        {
            get;
        }

        public virtual bool IsAutoCommandEnabled
        {
            get { return true; }
        }

        public virtual bool IsUndoRedoExpected
        {
            get { return false; }
        }

        public virtual DefaultSettings DefaultSettings
        {
            get { return DefaultSettings.GVim74; }
        }

        public virtual bool UseDefaultCaret
        {
            get { return false; }
        }

        protected VimHost(
            IProtectedOperations protectedOperations,
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _protectedOperations = protectedOperations;
            _textBufferFactoryService = textBufferFactoryService;
            _textEditorFactoryService = textEditorFactoryService;
            _textDocumentFactoryService = textDocumentFactoryService;
            _editorOperationsFactoryService = editorOperationsFactoryService;
        }

        public virtual void EnsurePackageLoaded()
        {
        }

        public virtual void Beep()
        {
            SystemSounds.Beep.Play();
        }

        public virtual void BeginBulkOperation()
        {
            // Host specific decision on how to respond
        }

        public virtual void Close(ITextView textView)
        {
            textView.Close();
        }

        public abstract void CloseAllOtherTabs(ITextView textView);

        public abstract void CloseAllOtherWindows(ITextView textView);

        /// <summary>
        /// Create a hidden ITextView.  It will have no roles in order to keep it out of 
        /// most plugins
        /// </summary>
        public virtual ITextView CreateHiddenTextView()
        {
            return _textEditorFactoryService.CreateTextView(
                _textBufferFactoryService.CreateTextBuffer(),
                _textEditorFactoryService.NoRoles);
        }

        public virtual void EndBulkOperation()
        {
            // Host specific decision on how to respond
        }

        /// <summary>
        /// Ensure the given SnapshotPoint is visible on the screen
        /// </summary>
        public void EnsureVisible(ITextView textView, SnapshotPoint point)
        {
            try
            {
                // Intentionally breaking up these tasks into different steps.  The act of making the 
                // line visible can actually invalidate the ITextViewLine instance and cause it to 
                // throw when we access it for making the point visible.  Breaking it into separate
                // steps so each one has to requery the current and valid information
                EnsureLineVisible(textView, point);
                EnsureLinePointVisible(textView, point);
            }
            catch (Exception)
            {
                // The ITextViewLine implementation can throw if this code runs in the middle of 
                // a layout or if the line believes itself to be invalid.  Hard to completely guard
                // against this
            }
        }

        public virtual void FindInFiles(string pattern, bool matchCase, string filesOfType, VimGrepFlags flags, FSharpFunc<Unit, Unit> action)
        {
            // Host specific decision on how to respond
        }

        public abstract void FormatLines(ITextView textView, SnapshotLineRange range);

        public virtual FSharpOption<int> GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            return FSharpOption<int>.None;
        }

        public abstract string GetName(ITextBuffer value);

        public abstract int GetTabIndex(ITextView textView);

        public virtual WordWrapStyles GetWordWrapStyle(ITextView textView)
        {
            return WordWrapStyles.VisibleGlyphs | WordWrapStyles.WordWrap;
        }

        public virtual bool TryGetFocusedTextView(out ITextView textView)
        {
            textView = _textViewList.FirstOrDefault(x => x.HasAggregateFocus);
            return textView != null;
        }

        public abstract bool GoToDefinition();

        public abstract bool GoToGlobalDeclaration(ITextView textView, string name);

        public abstract bool GoToLocalDeclaration(ITextView textView, string name);

        public abstract void GoToTab(int index);

        public abstract void OpenListWindow(ListKind listKind);

        public bool OpenLink(string link)
        {
            try
            {
                Process.Start(link);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public abstract FSharpOption<ListItem> NavigateToListItem(ListKind listKind, NavigationKind navigationKind, FSharpOption<int> argument, bool hasBang);

        public virtual bool IsDirty(ITextBuffer textBuffer)
        {
            // If this is an IProjectionBuffer then we need to dig into the actual ITextBuffer values
            // which make it up.  
            foreach (var sourceTextBuffer in TextBufferUtil.GetSourceBuffersRecursive(textBuffer))
            {
                // The inert buffer doesn't need to be considered.  It's used as a fake buffer by web applications
                // in order to render projected content
                if (sourceTextBuffer.ContentType == _textBufferFactoryService.InertContentType)
                {
                    continue;
                }

                if (_textDocumentFactoryService.TryGetTextDocument(sourceTextBuffer, out ITextDocument document) && document.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Perform the specified action when the specified text view is ready
        /// </summary>
        /// <param name="textView"></param>
        /// <param name="action"></param>
        public virtual void DoActionWhenTextViewReady(FSharpFunc<Unit, Unit> action, ITextView textView)
        {
            // Local functions to do the action.
            void doAction()
            {
                // Perform action if the text view is still open.
                if (!textView.IsClosed && !textView.InLayout)
                {
                    action.Invoke(null);
                }
            }
            void doActionHandler(object sender, RoutedEventArgs e)
            {
                // Unsubscribe.
                if (sender is FrameworkElement element)
                {
                    element.Loaded -= doActionHandler;
                }

                // Then schedule the action.
                _protectedOperations.BeginInvoke(doAction, DispatcherPriority.Loaded);
            }

            if (textView is IWpfTextView wpfTextView && !wpfTextView.VisualElement.IsLoaded)
            {
                // FrameworkElement.Loaded Event:
                //
                // Occurs when a FrameworkElement has been constructed and
                // added to the object tree, and is ready for interaction.
                wpfTextView.VisualElement.Loaded += doActionHandler;
            }
            else
            {
                // If the element is already loaded, do the action immediately.
                doAction();
            }
        }

        /// <summary>
        /// Default to seeing if the entire text buffer area is read only
        /// </summary>
        public virtual bool IsReadOnly(ITextBuffer textBuffer)
        {
            var span = new Span(0, textBuffer.CurrentSnapshot.Length);
            return textBuffer.IsReadOnly(span);
        }

        public virtual bool IsFocused(ITextView textView)
        {
            return textView.HasAggregateFocus;
        }

        /// <summary>
        /// Determine if the ITextView is visible.  Use the Wpf UIElement::IsVisible property
        /// to validate.  If this is not backed by an IWpfTextView then this will default to
        /// true
        /// </summary>
        public virtual bool IsVisible(ITextView textView)
        {
            if (textView is IWpfTextView wpfTextView)
            {
                return wpfTextView.VisualElement.IsVisible;
            }

            return true;
        }

        public abstract bool LoadFileIntoExistingWindow(string filePath, ITextView textView);

        public abstract FSharpOption<ITextView> LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column);

        public abstract void Make(bool jumpToFirstError, string arguments);

        public abstract void GoToWindow(ITextView textView, WindowKind windowKind, int count);

        public abstract bool NavigateTo(VirtualSnapshotPoint point);

        public virtual void Quit()
        {
            Application.Current.Shutdown();
        }

        public virtual bool Reload(ITextView textView)
        {
            if (!_textDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out ITextDocument document))
            {
                return false;
            }

            var result = document.Reload(EditOptions.DefaultMinimalChange);
            return result == ReloadResult.Succeeded || result == ReloadResult.SucceededWithCharacterSubstitutions;
        }

        /// <summary>
        /// Run the specified command on the supplied input, capture it's output and
        /// return it to the caller
        /// </summary>
        public virtual RunCommandResults RunCommand(string workingDirectory, string command, string arguments, string input)
        {
            // Use a (generous) timeout since we have no way to interrupt it.
            var timeout = 30 * 1000;

            // Avoid redirection for the 'start' command.
            var doRedirect = !arguments.StartsWith("/c start ", StringComparison.CurrentCultureIgnoreCase);

            // Populate the start info.
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = doRedirect,
                RedirectStandardOutput = doRedirect,
                RedirectStandardError = doRedirect,
                CreateNoWindow = true,
            };

            // Start the process and tasks to manage the I/O.
            try
            {
                var process = Process.Start(startInfo);
                if (doRedirect)
                {
                    var stdin = process.StandardInput;
                    var stdout = process.StandardOutput;
                    var stderr = process.StandardError;
                    var stdinTask = Task.Run(() => { stdin.Write(input); stdin.Close(); });
                    var stdoutTask = Task.Run(() => stdout.ReadToEnd());
                    var stderrTask = Task.Run(() => stderr.ReadToEnd());
                    if (process.WaitForExit(timeout))
                    {
                        return new RunCommandResults(process.ExitCode, stdoutTask.Result, stderrTask.Result);
                    }
                }
                else
                {
                    if (process.WaitForExit(timeout))
                    {
                        return new RunCommandResults(process.ExitCode, String.Empty, String.Empty);
                    }
                }
                throw new TimeoutException();
            }
            catch (Exception ex)
            {
                return new RunCommandResults(-1, "", ex.Message);
            }
        }

        public abstract void RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime);

        public abstract void RunHostCommand(ITextView textView, string command, string argument);

        public virtual bool Save(ITextBuffer textBuffer)
        {
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out ITextDocument document))
            {
                return false;
            }

            document.Save();
            return true;
        }

        /// <summary>
        /// By default anything but a pure interactive window is eligable for creation 
        /// </summary>
        public virtual bool ShouldCreateVimBuffer(ITextView textView)
        {
            if (textView.Roles.Contains(PredefinedTextViewRoles.Interactive) &&
                !textView.Roles.Contains(PredefinedTextViewRoles.Document))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// By default the host will only load vsvimrc files
        /// </summary>
        public virtual bool ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            return vimRcPath.VimRcKind == VimRcKind.VsVimRc;
        }

        public virtual bool SaveTextAs(string text, string filePath)
        {
            try
            {
                File.WriteAllText(filePath, text);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public abstract void SplitViewHorizontally(ITextView value);

        public abstract void SplitViewVertically(ITextView value);

        public virtual void StartShell(string workingDirectory, string command, string arguments)
        {
            // Populate the start info.
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = workingDirectory,
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
            };

            // Start the process.
            try
            {
                var process = Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public virtual void VimCreated(IVim vim)
        {
        }

        public virtual void VimDataCreated(IVimData vimData)
        {
        }

        public virtual void VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
        }

        /// <summary>
        /// Custom processing of an insert command is a host specific operation.  By default
        /// no custom processing is done
        /// </summary>
        public virtual bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            return false;
        }

        protected void RaiseIsVisibleChanged(ITextView textView)
        {
            if (_isVisibleChanged != null)
            {
                var args = new TextViewEventArgs(textView);
                _isVisibleChanged(this, args);
            }
        }

        protected void RaiseActiveTextViewChanged(FSharpOption<ITextView> oldTextView, FSharpOption<ITextView> newTextView)
        {
            if (_activeTextViewChanged != null)
            {
                var args = new TextViewChangedEventArgs(oldTextView, newTextView);
                _activeTextViewChanged(this, args);
            }
        }

        protected void RaiseBeforeSave(ITextBuffer textBuffer)
        {
            if (_beforeSave != null)
            {
                var args = new BeforeSaveEventArgs(textBuffer);
                _beforeSave(this, args);
            }
        }

        /// <summary>
        /// Do the vertical scrolling necessary to make sure the line is visible
        /// </summary>

        private void EnsureLineVisible(ITextView textView, SnapshotPoint point)
        {
            const double roundOff = 0.01;
            var textViewLine = textView.GetTextViewLineContainingBufferPosition(point);
            if (textViewLine == null)
            {
                return;
            }

            switch (textViewLine.VisibilityState)
            {
                case VisibilityState.FullyVisible:
                    // If the line is fully visible then no scrolling needs to occur
                    break;

                case VisibilityState.Hidden:
                case VisibilityState.PartiallyVisible:
                    {
                        ViewRelativePosition? pos = null;
                        if (textViewLine.Height <= textView.ViewportHeight + roundOff)
                        {
                            // The line fits into the view.  Figure out if it needs to be at the top 
                            // or the bottom
                            pos = textViewLine.Top < textView.ViewportTop
                                ? ViewRelativePosition.Top
                                : ViewRelativePosition.Bottom;
                        }
                        else if (textViewLine.Bottom < textView.ViewportBottom)
                        {
                            // Line does not fit into view but we can use more space at the bottom 
                            // of the view
                            pos = ViewRelativePosition.Bottom;
                        }
                        else if (textViewLine.Top > textView.ViewportTop)
                        {
                            pos = ViewRelativePosition.Top;
                        }

                        if (pos.HasValue)
                        {
                            textView.DisplayTextLineContainingBufferPosition(point, 0.0, pos.Value);
                        }
                    }
                    break;
                case VisibilityState.Unattached:
                    {
                        var pos = textViewLine.Start < textView.TextViewLines.FormattedSpan.Start && textViewLine.Height <= textView.ViewportHeight + roundOff
                                      ? ViewRelativePosition.Top
                                      : ViewRelativePosition.Bottom;
                        textView.DisplayTextLineContainingBufferPosition(point, 0.0, pos);
                    }
                    break;
            }
        }

        /// <summary>
        /// Do the horizontal scrolling necessary to make the column of the given point visible
        /// </summary>
        private void EnsureLinePointVisible(ITextView textView, SnapshotPoint point)
        {
            var textViewLine = textView.GetTextViewLineContainingBufferPosition(point);
            if (textViewLine == null)
            {
                return;
            }

            const double horizontalPadding = 2.0;
            const double scrollbarPadding = 200.0;
            var scroll = Math.Max(
                horizontalPadding,
                Math.Min(scrollbarPadding, textView.ViewportWidth / 4));
            var bounds = textViewLine.GetCharacterBounds(point);
            if (bounds.Left - horizontalPadding < textView.ViewportLeft)
            {
                textView.ViewportLeft = bounds.Left - scroll;
            }
            else if (bounds.Right + horizontalPadding > textView.ViewportRight)
            {
                textView.ViewportLeft = (bounds.Right + scroll) - textView.ViewportWidth;
            }
        }

        #region IWpfTextViewCreationListener

        /// <summary>
        /// Need to track the open ITextView values
        /// </summary>
        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            _textViewList.Add(textView);

            DependencyPropertyChangedEventHandler isVisibleHandler = delegate
            {
                RaiseIsVisibleChanged(textView);
            };
            textView.VisualElement.IsVisibleChanged += isVisibleHandler;

            textView.Closed += delegate
            {
                _textViewList.Remove(textView);
                textView.VisualElement.IsVisibleChanged -= isVisibleHandler;
            };
        }

        #endregion

        #region IVimHost

        bool IVimHost.AutoSynchronizeSettings
        {
            get { return AutoSynchronizeSettings; }
        }

        string IVimHost.HostIdentifier
        {
            get { return HostIdentifier; }
        }

        bool IVimHost.IsAutoCommandEnabled
        {
            get { return IsAutoCommandEnabled; }
        }

        bool IVimHost.IsUndoRedoExpected
        {
            get { return IsUndoRedoExpected; }
        }

        DefaultSettings IVimHost.DefaultSettings
        {
            get { return DefaultSettings; }
        }

        bool IVimHost.UseDefaultCaret
        {
            get { return UseDefaultCaret; }
        }

        void IVimHost.EnsurePackageLoaded()
        {
            EnsurePackageLoaded();
        }

        void IVimHost.Beep()
        {
            Beep();
        }

        void IVimHost.BeginBulkOperation()
        {
            BeginBulkOperation();
        }

        void IVimHost.Close(ITextView value)
        {
            Close(value);
        }

        ITextView IVimHost.CreateHiddenTextView()
        {
            return CreateHiddenTextView();
        }

        void IVimHost.EndBulkOperation()
        {
            EndBulkOperation();
        }

        void IVimHost.EnsureVisible(ITextView textView, SnapshotPoint point)
        {
            EnsureVisible(textView, point);
        }

        void IVimHost.FindInFiles(string pattern, bool matchCase, string filesOfType, VimGrepFlags flags, FSharpFunc<Unit, Unit> action)
        {
            FindInFiles(pattern, matchCase, filesOfType, flags, action);
        }

        void IVimHost.FormatLines(ITextView textView, SnapshotLineRange range)
        {
            FormatLines(textView, range);
        }

        FSharpOption<int> IVimHost.GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            return GetNewLineIndent(textView, contextLine, newLine, localSettings);
        }

        FSharpOption<ITextView> IVimHost.GetFocusedTextView()
        {
            return TryGetFocusedTextView(out ITextView textView)
                ? FSharpOption.Create(textView)
                : FSharpOption<ITextView>.None;
        }

        string IVimHost.GetName(ITextBuffer textBuffer)
        {
            return GetName(textBuffer);
        }

        int IVimHost.GetTabIndex(ITextView textView)
        {
            return GetTabIndex(textView);
        }

        WordWrapStyles IVimHost.GetWordWrapStyle(ITextView textView)
        {
            return GetWordWrapStyle(textView);
        }

        bool IVimHost.GoToDefinition()
        {
            return GoToDefinition();
        }

        bool IVimHost.OpenLink(string link)
        {
            return OpenLink(link);
        }

        bool IVimHost.GoToGlobalDeclaration(ITextView textView, string identifier)
        {
            return GoToGlobalDeclaration(textView, identifier);
        }

        bool IVimHost.GoToLocalDeclaration(ITextView textView, string identifier)
        {
            return GoToLocalDeclaration(textView, identifier);
        }

        void IVimHost.GoToTab(int index)
        {
            GoToTab(index);
        }

        FSharpOption<ListItem> IVimHost.NavigateToListItem(ListKind listKind, NavigationKind navigationKind, FSharpOption<int> argument, bool hasBang)
        {
            return NavigateToListItem(listKind, navigationKind, argument, hasBang);
        }

        bool IVimHost.IsDirty(ITextBuffer textBuffer)
        {
            return IsDirty(textBuffer);
        }

        void IVimHost.DoActionWhenTextViewReady(FSharpFunc<Unit, Unit> action, ITextView textView)
        {
            DoActionWhenTextViewReady(action, textView);
        }

        bool IVimHost.IsReadOnly(ITextBuffer textBuffer)
        {
            return IsReadOnly(textBuffer);
        }

        bool IVimHost.LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return LoadFileIntoExistingWindow(filePath, textView);
        }

        FSharpOption<ITextView> IVimHost.LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
        {
            return LoadFileIntoNewWindow(filePath, line, column);
        }

        void IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            Make(jumpToFirstError, arguments);
        }

        void IVimHost.GoToWindow(ITextView textView, WindowKind windowKind, int count)
        {
            GoToWindow(textView, windowKind, count);
        }

        void IVimHost.OpenListWindow(ListKind listKind)
        {
            OpenListWindow(listKind);
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            return NavigateTo(point);
        }

        void IVimHost.Quit()
        {
            Quit();
        }

        bool IVimHost.Reload(ITextView textView)
        {
            return Reload(textView);
        }

        RunCommandResults IVimHost.RunCommand(string workingDirectory, string command, string arguments, string input)
        {
            return RunCommand(workingDirectory, command, arguments, input);
        }

        void IVimHost.RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            RunCSharpScript(vimBuffer, callInfo, createEachTime);
        }

        void IVimHost.RunHostCommand(ITextView textView, string command, string argument)
        {
            RunHostCommand(textView, command, argument);
        }

        bool IVimHost.Save(ITextBuffer value)
        {
            return Save(value);
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            return SaveTextAs(text, filePath);
        }

        bool IVimHost.ShouldCreateVimBuffer(ITextView textView)
        {
            return ShouldCreateVimBuffer(textView);
        }

        bool IVimHost.ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            return ShouldIncludeRcFile(vimRcPath);
        }

        void IVimHost.SplitViewHorizontally(ITextView value)
        {
            SplitViewHorizontally(value);
        }

        void IVimHost.SplitViewVertically(ITextView value)
        {
            SplitViewVertically(value);
        }

        void IVimHost.StartShell(string workingDirectory, string command, string arguments)
        {
            StartShell(workingDirectory, command, arguments);
        }

        bool IVimHost.TryCustomProcess(ITextView textView, InsertCommand command)
        {
            return TryCustomProcess(textView, command);
        }

        bool IVimHost.IsVisible(ITextView textView)
        {
            return IsVisible(textView);
        }

        bool IVimHost.IsFocused(ITextView textView)
        {
            return IsFocused(textView);
        }

        void IVimHost.VimCreated(IVim vim)
        {
            VimCreated(vim);
        }

        void IVimHost.CloseAllOtherTabs(ITextView textView)
        {
            CloseAllOtherTabs(textView);
        }

        void IVimHost.CloseAllOtherWindows(ITextView textView)
        {
            CloseAllOtherWindows(textView);
        }

        void IVimHost.VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            VimRcLoaded(vimRcState, localSettings, windowSettings);
        }

        event EventHandler<TextViewEventArgs> IVimHost.IsVisibleChanged
        {
            add { _isVisibleChanged += value; }
            remove { _isVisibleChanged -= value; }
        }

        event EventHandler<TextViewChangedEventArgs> IVimHost.ActiveTextViewChanged
        {
            add { _activeTextViewChanged += value; }
            remove { _activeTextViewChanged -= value; }
        }

        event EventHandler<BeforeSaveEventArgs> IVimHost.BeforeSave
        {
            add { _beforeSave += value; }
            remove { _beforeSave -= value; }
        }

        #endregion
    }
}
