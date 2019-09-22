using System;
using Microsoft.FSharp.Core;
using Vim;
using Vim.Interpreter;

namespace Vim.Mac
{
    public class VimHost : IVimHost
    {
        public bool AutoSynchronizeSettings => throw new NotImplementedException();

        public DefaultSettings DefaultSettings => throw new NotImplementedException();

        public string HostIdentifier => throw new NotImplementedException();

        public bool IsAutoCommandEnabled => throw new NotImplementedException();

        public bool IsUndoRedoExpected => throw new NotImplementedException();

        public int TabCount => throw new NotImplementedException();

        public bool UseDefaultCaret => throw new NotImplementedException();

        public event EventHandler<TextViewEventArgs> IsVisibleChanged;
        public event EventHandler<TextViewChangedEventArgs> ActiveTextViewChanged;
        public event EventHandler<BeforeSaveEventArgs> BeforeSave;

        public void Beep()
        {
            throw new NotImplementedException();
        }

        public void BeginBulkOperation()
        {
            throw new NotImplementedException();
        }

        public void Close(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            throw new NotImplementedException();
        }

        public void CloseAllOtherTabs(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            throw new NotImplementedException();
        }

        public void CloseAllOtherWindows(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            throw new NotImplementedException();
        }

        public Microsoft.VisualStudio.Text.Editor.ITextView CreateHiddenTextView()
        {
            throw new NotImplementedException();
        }

        public void DoActionWhenTextViewReady(FSharpFunc<Unit, Unit> action, Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public void EndBulkOperation()
        {
            throw new NotImplementedException();
        }

        public void EnsurePackageLoaded()
        {
            throw new NotImplementedException();
        }

        public void EnsureVisible(Microsoft.VisualStudio.Text.Editor.ITextView textView, Microsoft.VisualStudio.Text.SnapshotPoint point)
        {
            throw new NotImplementedException();
        }

        public void FindInFiles(string pattern, bool matchCase, string filesOfType, VimGrepFlags flags, FSharpFunc<Unit, Unit> action)
        {
            throw new NotImplementedException();
        }

        public void FormatLines(Microsoft.VisualStudio.Text.Editor.ITextView textView, SnapshotLineRange range)
        {
            throw new NotImplementedException();
        }

        public FSharpOption<Microsoft.VisualStudio.Text.Editor.ITextView> GetFocusedTextView()
        {
            throw new NotImplementedException();
        }

        public string GetName(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public FSharpOption<int> GetNewLineIndent(Microsoft.VisualStudio.Text.Editor.ITextView textView, Microsoft.VisualStudio.Text.ITextSnapshotLine contextLine, Microsoft.VisualStudio.Text.ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            throw new NotImplementedException();
        }

        public int GetTabIndex(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public Microsoft.VisualStudio.Text.Editor.WordWrapStyles GetWordWrapStyle(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool GoToDefinition()
        {
            throw new NotImplementedException();
        }

        public bool GoToGlobalDeclaration(Microsoft.VisualStudio.Text.Editor.ITextView tetxView, string identifier)
        {
            throw new NotImplementedException();
        }

        public bool GoToLocalDeclaration(Microsoft.VisualStudio.Text.Editor.ITextView textView, string identifier)
        {
            throw new NotImplementedException();
        }

        public void GoToTab(int index)
        {
            throw new NotImplementedException();
        }

        public void GoToWindow(Microsoft.VisualStudio.Text.Editor.ITextView textView, WindowKind direction, int count)
        {
            throw new NotImplementedException();
        }

        public bool IsDirty(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public bool IsFocused(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public bool IsVisible(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool LoadFileIntoExistingWindow(string filePath, Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public FSharpOption<Microsoft.VisualStudio.Text.Editor.ITextView> LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
        {
            throw new NotImplementedException();
        }

        public void Make(bool jumpToFirstError, string arguments)
        {
            throw new NotImplementedException();
        }

        public bool NavigateTo(Microsoft.VisualStudio.Text.VirtualSnapshotPoint point)
        {
            throw new NotImplementedException();
        }

        public FSharpOption<ListItem> NavigateToListItem(ListKind listKind, NavigationKind navigationKind, FSharpOption<int> argument, bool hasBang)
        {
            throw new NotImplementedException();
        }

        public bool OpenLink(string link)
        {
            throw new NotImplementedException();
        }

        public void OpenListWindow(ListKind listKind)
        {
            throw new NotImplementedException();
        }

        public void Quit()
        {
            throw new NotImplementedException();
        }

        public bool Reload(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public RunCommandResults RunCommand(string workingDirectory, string file, string arguments, string input)
        {
            throw new NotImplementedException();
        }

        public void RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            throw new NotImplementedException();
        }

        public void RunHostCommand(Microsoft.VisualStudio.Text.Editor.ITextView textView, string commandName, string argument)
        {
            throw new NotImplementedException();
        }

        public bool Save(Microsoft.VisualStudio.Text.ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public bool SaveTextAs(string text, string filePath)
        {
            throw new NotImplementedException();
        }

        public bool ShouldCreateVimBuffer(Microsoft.VisualStudio.Text.Editor.ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            throw new NotImplementedException();
        }

        public void SplitViewHorizontally(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            throw new NotImplementedException();
        }

        public void SplitViewVertically(Microsoft.VisualStudio.Text.Editor.ITextView value)
        {
            throw new NotImplementedException();
        }

        public void StartShell(string workingDirectory, string file, string arguments)
        {
            throw new NotImplementedException();
        }

        public bool TryCustomProcess(Microsoft.VisualStudio.Text.Editor.ITextView textView, InsertCommand command)
        {
            throw new NotImplementedException();
        }

        public void VimCreated(IVim vim)
        {
            throw new NotImplementedException();
        }

        public void VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            throw new NotImplementedException();
        }
    }
}
