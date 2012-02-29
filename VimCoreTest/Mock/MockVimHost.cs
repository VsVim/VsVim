﻿using System;
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
        private event EventHandler<TextViewEventArgs> _isVisibleChanged;

        public int BeepCount { get; set; }
        public int GoToDefinitionCount { get; set; }
        public bool GoToFileReturn { get; set; }
        public bool GoToDefinitionReturn { get; set; }
        public bool IsCompletionWindowActive { get; set; }
        public int DismissCompletionWindowCount { get; set; }
        public VirtualSnapshotPoint NavigateToData { get; set; }
        public bool NavigateToReturn { get; set; }
        public int ShowOpenFileDialogCount { get; set; }
        public ITextView FocusedTextView { get; set; }
        public FSharpList<IVimBuffer> Buffers { get; set; }
        public bool? IsTextViewVisible { get; set; }
        public Func<ITextView, InsertCommand, bool> TryCustomProcessFunc { get; set; }
        public Func<ITextView> CreateHiddenTextViewFunc { get; set; }
        public Func<string, string, IVimData, string> RunCommandFunc { get; set; }
        public Action<string, string> RunVisualStudioCommandFunc { get; set; }

        /// <summary>
        /// Data from the last GoToNextTab call
        /// </summary>
        public Tuple<Path, int> GoToNextTabData { get; set; }

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
            GoToDefinitionReturn = true;
            IsCompletionWindowActive = false;
            NavigateToReturn = false;
            Buffers = FSharpList<IVimBuffer>.Empty;
            BeepCount = 0;
            GoToDefinitionCount = 0;
            IsTextViewVisible = null;
            _isVisibleChanged = null;
            TryCustomProcessFunc = null;
            CreateHiddenTextViewFunc = delegate { throw new NotImplementedException(); };
            RunCommandFunc = delegate { throw new NotImplementedException(); };
            RunVisualStudioCommandFunc = delegate { throw new NotImplementedException(); };
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
            return String.Empty;
        }

        void IVimHost.ShowOpenFileDialog()
        {
            ShowOpenFileDialogCount++;
        }

        void IVimHost.Close(ITextView textView)
        {
            throw new NotImplementedException();
        }

        ITextView IVimHost.CreateHiddenTextView()
        {
            return CreateHiddenTextViewFunc();
        }

        bool IVimHost.Save(ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.SaveTextAs(string text, string filePath)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.SplitViewHorizontally(ITextView textView)
        {
            throw new NotImplementedException();
        }

        HostResult IVimHost.Make(bool jumpToFirstError, string arguments)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewDown(ITextView textView)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewUp(ITextView textView)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.GoToGlobalDeclaration(ITextView value, string target)
        {
            throw new NotImplementedException();
        }

        bool IVimHost.GoToLocalDeclaration(ITextView value, string target)
        {
            throw new NotImplementedException();
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

        void IVimHost.GoToNextTab(Path value, int count)
        {
            GoToNextTabData = Tuple.Create(value, count);
        }

        void IVimHost.GoToTab(int index)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewLeft(ITextView value)
        {
            throw new NotImplementedException();
        }

        void IVimHost.MoveViewRight(ITextView value)
        {
            throw new NotImplementedException();
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


        void IVimHost.BeginBulkOperation()
        {

        }

        void IVimHost.EndBulkOperation()
        {

        }
    }
}
