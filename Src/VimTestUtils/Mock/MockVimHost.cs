﻿using System;
using Vim.EditorHost;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Vim.Extensions;

namespace Vim.UnitTest.Mock
{
    public class MockVimHost : IVimHost
    {
        public static readonly object FileNameKey = new object();

        private event EventHandler<TextViewEventArgs> _isVisibleChanged;
#pragma warning disable 67
        private event EventHandler<TextViewChangedEventArgs> _activeTextViewChanged;
        private event EventHandler<BeforeSaveEventArgs> _beforeSave;
#pragma warning restore 67

        public bool AutoSynchronizeSettings { get; set; }
        public bool IsAutoCommandEnabled { get; set; }
        public bool IsUndoRedoExpected { get; set; }
        public DefaultSettings DefaultSettings { get; set; }
        public bool EnsuredPackageLoaded { get; private set; }
        public int BeepCount { get; set; }
        public bool ClosedOtherWindows { get; private set; }
        public bool ClosedOtherTabs { get; private set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToFileReturn { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public Func<ITextView, ITextSnapshotLine, ITextSnapshotLine, IVimLocalSettings, FSharpOption<int>> GetNewLineIndentFunc { get; set; }
        public Func<ITextView, string, bool> GoToLocalDeclarationFunc { get; set; }
        public Func<ITextView, string, bool> GoToGlobalDeclarationFunc { get; set; }
        public Func<ITextView, bool> ReloadFunc { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public VirtualSnapshotPoint NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }
        public ITextView FocusedTextView { get; set; }
        public FSharpList<IVimBuffer> Buffers { get; set; }
        public bool? IsTextViewVisible { get; set; }
        public Func<ITextView, InsertCommand, bool> TryCustomProcessFunc { get; set; }
        public Func<ITextView> CreateHiddenTextViewFunc { get; set; }
        public Func<ITextBuffer, bool> IsDirtyFunc { get; set; }
        public Func<string, string, string, string, RunCommandResults> RunCommandFunc { get; set; }
        public Action<ITextView, string, string> RunHostCommandFunc { get; set; }
        public Func<string, FSharpOption<int>, FSharpOption<int>, bool> LoadIntoNewWindowFunc { get; set; }
        public Action<QuickFix, int, bool> RunQuickFixFunc { get; set; }
        public Action OpenQuickFixWindowFunc { get; set; }
        public Func<string, string, bool> RunSaveTextAs { get; set; }
        public ITextBuffer LastSaved { get; set; }
        public ITextView LastClosed { get; set; }
        public bool ShouldCreateVimBufferImpl { get; set; }
        public bool ShouldIncludeRcFile { get; set; }
        public VimRcState VimRcState { get; private set; }
        public int TabCount { get; set; }
        public int GoToTabData { get; set; }
        public int GetTabIndexData { get; set; }
        public WordWrapStyles WordWrapStyle { get; set; }
        public bool UseDefaultCaret { get; set; }

        public MockVimHost()
        {
            Clear();
        }

        public void RaiseIsVisibleChanged(ITextView textView)
        {
            if (_isVisibleChanged != null)
            {
                var args = new TextViewEventArgs(textView);
                _isVisibleChanged(this, args);
            }
        }

        /// <summary>
        /// Clear out the stored information
        /// </summary>
        public void Clear()
        {
            AutoSynchronizeSettings = true;
            IsAutoCommandEnabled = true;
            IsUndoRedoExpected = false;
            DefaultSettings = DefaultSettings.GVim74;
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
            Buffers = FSharpList<IVimBuffer>.Empty;
            BeepCount = 0;
            ClosedOtherWindows = false;
            ClosedOtherTabs = false;
            GoToDefinitionCount = 0;
            FocusedTextView = null;
            IsTextViewVisible = null;
            _isVisibleChanged = null;
            TryCustomProcessFunc = null;
            GetNewLineIndentFunc = delegate { return FSharpOption<int>.None; };
            GoToLocalDeclarationFunc = delegate { throw new NotImplementedException(); };
            GoToGlobalDeclarationFunc = delegate { throw new NotImplementedException(); };
            CreateHiddenTextViewFunc = delegate { throw new NotImplementedException(); };
            RunCommandFunc = delegate { throw new NotImplementedException(); };
            RunHostCommandFunc = delegate { throw new NotImplementedException(); };
            LoadIntoNewWindowFunc = delegate { throw new NotImplementedException(); };
            RunQuickFixFunc = delegate { throw new NotImplementedException(); };
            OpenQuickFixWindowFunc = delegate { throw new NotImplementedException(); };
            RunSaveTextAs = delegate { throw new NotImplementedException(); };
            ReloadFunc = delegate { return true; };
            IsDirtyFunc = null;
            LastClosed = null;
            LastSaved = null;
            ShouldCreateVimBufferImpl = false;
            ShouldIncludeRcFile = true;
            WordWrapStyle = WordWrapStyles.WordWrap;
            UseDefaultCaret = false;
        }

        void IVimHost.EnsurePackageLoaded()
        {
            EnsuredPackageLoaded = true;
        }

        void IVimHost.Beep()
        {
            BeepCount++;
        }

        bool IVimHost.GoToDefinition()
        {
            GoToDefinitionCount++;
            return GoToDefinitionReturn;
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            NavigateToData = point;
            return NavigateToReturn;
        }

        string IVimHost.GetName(ITextBuffer textBuffer)
        {
            string name = null;
            if (textBuffer.Properties.TryGetPropertySafe(FileNameKey, out object value))
            {
                name = value as string;
            }

            return name ?? "";
        }

        void IVimHost.Close(ITextView textView)
        {
            LastClosed = textView;
            textView.Close();
        }

        void IVimHost.CloseAllOtherWindows(ITextView textView)
        {
            ClosedOtherWindows = true;
        }

        void IVimHost.CloseAllOtherTabs(ITextView textView)
        {
            ClosedOtherTabs = true;
        }

        ITextView IVimHost.CreateHiddenTextView()
        {
            return CreateHiddenTextViewFunc();
        }

        bool IVimHost.Save(ITextBuffer textBuffer)
        {
            LastSaved = textBuffer;
            return true;
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            return RunSaveTextAs(text, filePath);
        }

        void IVimHost.SplitViewHorizontally(ITextView textView)
        {
            throw new NotImplementedException();
        }

        void IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            throw new NotImplementedException();
        }

        void IVimHost.GoToWindow(ITextView textView, WindowKind windowKind, int count)
        {
            throw new NotImplementedException();
        }

        FSharpOption<int> IVimHost.GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            return GetNewLineIndentFunc(textView, contextLine, newLine, localSettings);
        }

        bool IVimHost.GoToGlobalDeclaration(ITextView value, string target)
        {
            return GoToGlobalDeclarationFunc(value, target);
        }

        bool IVimHost.GoToLocalDeclaration(ITextView value, string target)
        {
            return GoToLocalDeclarationFunc(value, target);
        }

        void IVimHost.FormatLines(ITextView value, SnapshotLineRange range)
        {
            throw new NotImplementedException();
        }

        void IVimHost.EnsureVisible(ITextView textView, SnapshotPoint value)
        {
        }

        bool IVimHost.IsDirty(ITextBuffer value)
        {
            if (IsDirtyFunc != null)
            {
                return IsDirtyFunc(value);
            }

            return false;
        }

        bool IVimHost.IsReadOnly(ITextBuffer value)
        {
            return false;
        }

        bool IVimHost.LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return true;
        }

        bool IVimHost.Reload(ITextView textView)
        {
            return ReloadFunc(textView);
        }

        void IVimHost.GoToTab(int index)
        {
            GoToTabData = index;
        }

        RunCommandResults IVimHost.RunCommand(string workingDirectory, string command, string arguments, string input)
        {
            return RunCommandFunc(workingDirectory, command, arguments, input);
        }

        void IVimHost.RunHostCommand(ITextView textView, string command, string argument)
        {
            RunHostCommandFunc(textView, command, argument);
        }

        void IVimHost.SplitViewVertically(ITextView value)
        {
            throw new NotImplementedException();
        }

        void IVimHost.StartShell(string workingDirectory, string command, string arguments)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
        {
            return LoadIntoNewWindowFunc(filePath, line, column);
        }

        FSharpOption<ITextView> IVimHost.GetFocusedTextView()
        {
            return FSharpOption.CreateForReference(FocusedTextView);
        }

        bool IVimHost.IsFocused(ITextView textView)
        {
            if (FocusedTextView != null)
            {
                return textView == FocusedTextView;
            }

            return true;
        }

        void IVimHost.Quit()
        {
            throw new NotImplementedException();
        }

        bool IVimHost.IsVisible(ITextView textView)
        {
            if (IsTextViewVisible.HasValue)
            {
                return IsTextViewVisible.Value;
            }

            return true;
        }

        bool IVimHost.TryCustomProcess(ITextView textView, InsertCommand command)
        {
            if (TryCustomProcessFunc != null)
            {
                return TryCustomProcessFunc(textView, command);
            }

            return false;
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

        void IVimHost.BeginBulkOperation()
        {
        }

        void IVimHost.EndBulkOperation()
        {
        }

        bool IVimHost.ShouldCreateVimBuffer(ITextView textView)
        {
            return ShouldCreateVimBufferImpl;
        }

        bool IVimHost.ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            return ShouldIncludeRcFile;
        }

        void IVimHost.OpenQuickFixWindow()
        {
            OpenQuickFixWindowFunc();
        }

        bool IVimHost.GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            RunQuickFixFunc(quickFix, count, hasBang);
            return false;
        }

        void IVimHost.VimCreated(IVim vim)
        {
        }

        void IVimHost.VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            VimRcState = vimRcState;
        }

        int IVimHost.GetTabIndex(ITextView textView)
        {
            return GetTabIndexData;
        }

        WordWrapStyles IVimHost.GetWordWrapStyle(ITextView textView)
        {
            return WordWrapStyle;
        }

        int IVimHost.TabCount
        {
            get { return TabCount; }
        }

        bool IVimHost.ShouldKeepSelectionAfterHostCommand(string command, string argument)
        {
            return false;
        }

        bool IVimHost.UseDefaultCaret
        {
            get { return UseDefaultCaret; }
        }

        bool IVimHost.HasMultipleCarets(ITextView textView)
        {
            return false;
        }
    }
}
