using System;
using EditorUtils;
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
#pragma warning restore 67

        public bool AutoSynchronizeSettings { get; set; }
        public DefaultSettings DefaultSettings { get; set; }
        public int BeepCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToFileReturn { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public Func<ITextView, string, bool> GoToLocalDeclarationFunc { get; set; }
        public Func<ITextView, string, bool> GoToGlobalDeclarationFunc { get; set; }
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
        public Func<string, string, IVimData, string> RunCommandFunc { get; set; }
        public Action<string, string> RunVisualStudioCommandFunc { get; set; }
        public Action<QuickFix, int, bool> RunQuickFixFunc { get; set; }
        public Func<string, string, bool> RunSaveTextAs { get; set; }
        public ITextBuffer LastSaved { get; set; }
        public ITextView LastClosed { get; set; }
        public bool ShouldCreateVimBufferImpl { get; set; }
        public VimRcState VimRcState { get; private set; }
        public int TabCount { get; set; }
        public IFontProperties FontProperties { get; set; }
        public int GoToTabData { get; set; }
        public int GetTabIndexData { get; set; }

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
            DefaultSettings = DefaultSettings.GVim74;
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
            Buffers = FSharpList<IVimBuffer>.Empty;
            BeepCount = 0;
            GoToDefinitionCount = 0;
            IsTextViewVisible = null;
            _isVisibleChanged = null;
            TryCustomProcessFunc = null;
            GoToLocalDeclarationFunc = delegate { throw new NotImplementedException(); };
            GoToGlobalDeclarationFunc = delegate { throw new NotImplementedException(); };
            CreateHiddenTextViewFunc = delegate { throw new NotImplementedException(); };
            RunCommandFunc = delegate { throw new NotImplementedException(); };
            RunVisualStudioCommandFunc = delegate { throw new NotImplementedException(); };
            RunQuickFixFunc = delegate { throw new NotImplementedException(); };
            RunSaveTextAs = delegate { throw new NotImplementedException(); };
            IsDirtyFunc = null;
            LastClosed = null;
            LastSaved = null;
            ShouldCreateVimBufferImpl = false;
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
            object value;
            if (textBuffer.Properties.TryGetPropertySafe(FileNameKey, out value))
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

        HostResult IVimHost.SplitViewHorizontally(ITextView textView)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.MoveFocus(ITextView textView, Direction direction)
        {
            throw new NotImplementedException();
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

        HostResult IVimHost.LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return HostResult.Success;
        }

        bool IVimHost.Reload(ITextBuffer value)
        {
            return true;
        }

        void IVimHost.GoToTab(int index)
        {
            GoToTabData = index;
        }

        string IVimHost.RunCommand(string command, string arguments, IVimData vimData)
        {
            return RunCommandFunc(command, arguments, vimData);
        }

        void IVimHost.RunVisualStudioCommand(string command, string argument)
        {
            RunVisualStudioCommandFunc(command, argument);
        }

        HostResult IVimHost.SplitViewVertically(ITextView value)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.LoadFileIntoNewWindow(string filePath)
        {
            throw new NotImplementedException();
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

        bool IVimHost.GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            RunQuickFixFunc(quickFix, count, hasBang);
            return false;
        }

        void IVimHost.VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            VimRcState = vimRcState;
        }

        int IVimHost.GetTabIndex(ITextView textView)
        {
            return GetTabIndexData;            
        }

        int IVimHost.TabCount
        {
            get { return TabCount; }
        }
    }
}
