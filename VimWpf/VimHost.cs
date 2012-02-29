﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            // Now that the line is visible we need to potentially do some horizontal scrolling
            // take make sure that it's on screen
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

        public abstract void FormatLines(ITextView textView, SnapshotLineRange range);

        public abstract string GetName(ITextBuffer value);

        public virtual FSharpOption<ITextView> GetFocusedTextView()
        {
            var textView = _textViewList.FirstOrDefault(x => x.HasAggregateFocus);
            return FSharpOption.CreateForReference(textView);
        }

        public abstract bool GoToDefinition();

        public abstract bool GoToGlobalDeclaration(ITextView textView, string name);

        public abstract bool GoToLocalDeclaration(ITextView textView, string name);

        public abstract void GoToNextTab(Path direction, int count);

        public abstract void GoToTab(int index);

        public virtual bool IsDirty(ITextBuffer textbuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textbuffer, out document))
            {
                return false;
            }

            return document.IsDirty;
        }

        /// <summary>
        /// Default to seeing if the entire text buffer area is read only
        /// </summary>
        public virtual bool IsReadOnly(ITextBuffer textBuffer)
        {
            var span = new Span(0, textBuffer.CurrentSnapshot.Length);
            return textBuffer.IsReadOnly(span);
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

        public abstract HostResult LoadFileIntoExistingWindow(string filePath, ITextView textView);

        public abstract HostResult LoadFileIntoNewWindow(string filePath);

        public abstract HostResult Make(bool jumpToFirstError, string arguments);

        public abstract void MoveViewDown(ITextView value);

        public abstract void MoveViewUp(ITextView value);

        public abstract void MoveViewLeft(ITextView value);

        public abstract void MoveViewRight(ITextView value);

        public abstract bool NavigateTo(VirtualSnapshotPoint point);

        public virtual void Quit()
        {
            Application.Current.Shutdown();
        }

        public virtual bool Reload(ITextBuffer textBuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document))
            {
                return false;
            }

            document.Reload(EditOptions.DefaultMinimalChange);
            return true;
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

        public abstract void RunVisualStudioCommand(string command, string argument);

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

        public abstract bool SaveTextAs(string text, string filePath);

        public abstract void ShowOpenFileDialog();

        public abstract HostResult SplitViewHorizontally(ITextView value);

        public abstract HostResult SplitViewVertically(ITextView value);

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

        #region IWpfTextViewCreationListener

        /// <summary>
        /// Need to track the open ITextView values
        /// </summary>
        void IWpfTextViewCreationListener.TextViewCreated(IWpfTextView textView)
        {
            _textViewList.Add(textView);

            DependencyPropertyChangedEventHandler isVisibleHandler = delegate {
                RaiseIsVisibleChanged(textView);
            };
            textView.VisualElement.IsVisibleChanged += isVisibleHandler;

            textView.Closed += delegate { 
                _textViewList.Remove(textView); 
                textView.VisualElement.IsVisibleChanged -= isVisibleHandler;
            };
        }

        #endregion

        #region IVimHost

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

        FSharpOption<ITextView> IVimHost.GetFocusedTextView()
        {
            return GetFocusedTextView();
        }

        string IVimHost.GetName(ITextBuffer textBuffer)
        {
            return GetName(textBuffer);
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

        void IVimHost.GoToNextTab(Path value, int count)
        {
            GoToNextTab(value, count);
        }

        void IVimHost.GoToTab(int index)
        {
            GoToTab(index);
        }

        bool IVimHost.IsDirty(ITextBuffer textBuffer)
        {
            return IsDirty(textBuffer);
        }

        bool IVimHost.IsReadOnly(ITextBuffer textBuffer)
        {
            return IsReadOnly(textBuffer);
        }

        HostResult IVimHost.LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return LoadFileIntoExistingWindow(filePath, textView);
        }

        HostResult IVimHost.LoadFileIntoNewWindow(string filePath)
        {
            return LoadFileIntoNewWindow(filePath);
        }

        HostResult IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            return Make(jumpToFirstError, arguments);
        }

        void IVimHost.MoveViewDown(ITextView value)
        {
            MoveViewDown(value);
        }

        void IVimHost.MoveViewLeft(ITextView value)
        {
            MoveViewLeft(value);
        }

        void IVimHost.MoveViewRight(ITextView value)
        {
            MoveViewRight(value);
        }

        void IVimHost.MoveViewUp(ITextView value)
        {
            MoveViewUp(value);
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            return NavigateTo(point);
        }

        void IVimHost.Quit()
        {
            Quit();
        }

        bool IVimHost.Reload(ITextBuffer value)
        {
            return Reload(value);
        }

        string IVimHost.RunCommand(string command, string arguments, IVimData vimData)
        {
            return RunCommand(command, arguments, vimData);
        }

        void IVimHost.RunVisualStudioCommand(string command, string argument)
        {
            RunVisualStudioCommand(command, argument);
        }

        bool IVimHost.Save(ITextBuffer value)
        {
            return Save(value);
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            return SaveTextAs(text, filePath);
        }

        void IVimHost.ShowOpenFileDialog()
        {
            ShowOpenFileDialog();
        }

        HostResult IVimHost.SplitViewHorizontally(ITextView value)
        {
            return SplitViewHorizontally(value);
        }

        HostResult IVimHost.SplitViewVertically(ITextView value)
        {
            return SplitViewVertically(value);
        }

        bool IVimHost.TryCustomProcess(ITextView textView, InsertCommand command)
        {
            return TryCustomProcess(textView, command);
        }

        bool IVimHost.IsVisible(ITextView textView)
        {
            return IsVisible(textView);
        }

        event EventHandler<TextViewEventArgs> IVimHost.IsVisibleChanged
        {
            add { _isVisibleChanged += value; }
            remove { _isVisibleChanged -= value; }
        }

        #endregion

    }
}
