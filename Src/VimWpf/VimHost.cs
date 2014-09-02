using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows;
using EditorUtils;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Vim.Extensions;
using Path = Vim.Path;

namespace Vim.UI.Wpf
{
    public abstract class VimHost : IVimHost, IWpfTextViewCreationListener
    {
        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly ITextEditorFactoryService _textEditorFactoryService;
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;
        private readonly IEditorOperationsFactoryService _editorOperationsFactoryService;
        private readonly List<ITextView> _textViewList = new List<ITextView>();
        private event EventHandler<TextViewEventArgs> _isVisibleChanged;
        private event EventHandler<TextViewChangedEventArgs> _activeTextViewChanged;

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

        public virtual bool AutoSynchronizeSettings
        {
            get { return true; }
        }

        public abstract int TabCount
        {
            get;
        }

        public virtual bool IsAutoCommandEnabled
        {
            get { return true; }
        }

        public virtual DefaultSettings DefaultSettings
        {
            get { return DefaultSettings.GVim73; }
        }

        protected VimHost(
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            _textBufferFactoryService = textBufferFactoryService;
            _textEditorFactoryService = textEditorFactoryService;
            _textDocumentFactoryService = textDocumentFactoryService;
            _editorOperationsFactoryService = editorOperationsFactoryService;
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

        public abstract void FormatLines(ITextView textView, SnapshotLineRange range);

        public virtual FSharpOption<int> GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine)
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

        public abstract bool GoToQuickFix(QuickFix quickFix, int count, bool hasBang);

        public virtual bool IsDirty(ITextBuffer textBuffer)
        {
            // If this is an IProjectionBuffer then we need to dig into the actual ITextBuffer values
            // which make it up.  
            foreach (var sourceTextBuffer in textBuffer.GetSourceBuffersRecursive())
            {
                // The inert buffer doesn't need to be considered.  It's used as a fake buffer by web applications
                // in order to render projected content
                if (sourceTextBuffer.ContentType == _textBufferFactoryService.InertContentType)
                {
                    continue;
                }

                ITextDocument document;
                if (_textDocumentFactoryService.TryGetTextDocument(sourceTextBuffer, out document) && document.IsDirty)
                {
                    return true;
                }
            }

            return false;
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
            var wpfTextView = textView as IWpfTextView;
            if (wpfTextView != null)
            {
                return wpfTextView.VisualElement.IsVisible;
            }

            return true;
        }

        public abstract bool LoadFileIntoExistingWindow(string filePath, ITextView textView);

        public abstract bool LoadFileIntoNewWindow(string filePath);

        public abstract void Make(bool jumpToFirstError, string arguments);

        public abstract void MoveFocus(ITextView textView, Direction direction);

        public abstract bool NavigateTo(VirtualSnapshotPoint point);

        public virtual void Quit()
        {
            Application.Current.Shutdown();
        }

        public virtual bool Reload(ITextView textView)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textView.TextDataModel.DocumentBuffer, out document))
            {
                return false;
            }

            var result = document.Reload(EditOptions.DefaultMinimalChange);
            return result == ReloadResult.Succeeded || result == ReloadResult.SucceededWithCharacterSubstitutions;
        }

        /// <summary>
        /// Run the specified command, capture it's output and return it to the caller
        /// </summary>
        public virtual string RunCommand(string command, string arguments, IVimData vimdata)
        {
            var startInfo = new ProcessStartInfo
                                {
                                    FileName = command,
                                    Arguments = arguments,
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false,
                                    WorkingDirectory = vimdata.CurrentDirectory
                                };
            try
            {
                var process = Process.Start(startInfo);
                process.WaitForExit();
                return process.StandardOutput.ReadToEnd();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public abstract void RunVisualStudioCommand(ITextView textView, string command, string argument);

        public virtual bool Save(ITextBuffer textBuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document))
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

        /// <summary>
        /// Do the vertical scrolling necessary to make sure the line is visible
        /// </summary>
        private void EnsureLineVisible(ITextView textView, SnapshotPoint point)
        {
            const double roundOff = 0.01;
            var textViewLine = textView.GetTextViewLineContainingBufferPosition(point);

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

        bool IVimHost.IsAutoCommandEnabled
        {
            get { return IsAutoCommandEnabled; }
        }

        DefaultSettings IVimHost.DefaultSettings
        {
            get { return DefaultSettings; }
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

        void IVimHost.FormatLines(ITextView textView, SnapshotLineRange range)
        {
            FormatLines(textView, range);
        }

        FSharpOption<int> IVimHost.GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine)
        {
            return GetNewLineIndent(textView, contextLine, newLine);
        }

        FSharpOption<ITextView> IVimHost.GetFocusedTextView()
        {
            ITextView textView;
            return TryGetFocusedTextView(out textView)
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

        bool IVimHost.GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            return GoToQuickFix(quickFix, count, hasBang);
        }

        bool IVimHost.IsDirty(ITextBuffer textBuffer)
        {
            return IsDirty(textBuffer);
        }

        bool IVimHost.IsReadOnly(ITextBuffer textBuffer)
        {
            return IsReadOnly(textBuffer);
        }

        bool IVimHost.LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return LoadFileIntoExistingWindow(filePath, textView);
        }

        bool IVimHost.LoadFileIntoNewWindow(string filePath)
        {
            return LoadFileIntoNewWindow(filePath);
        }

        void IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            Make(jumpToFirstError, arguments);
        }

        void IVimHost.MoveFocus(ITextView textView, Direction direction)
        {
            MoveFocus(textView, direction);
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

        string IVimHost.RunCommand(string command, string arguments, IVimData vimData)
        {
            return RunCommand(command, arguments, vimData);
        }

        void IVimHost.RunVisualStudioCommand(ITextView textView, string command, string argument)
        {
            RunVisualStudioCommand(textView, command, argument);
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

        #endregion
    }
}
