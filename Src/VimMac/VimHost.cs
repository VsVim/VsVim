using System;
using System.ComponentModel.Composition;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Vim;
using Vim.Interpreter;
using MonoDevelop.Core;
using MonoDevelop.Ide;

namespace Vim.Mac
{
    [Export(typeof(IVimHost))]
    [Export(typeof(VimCocoaHost))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    public class VimCocoaHost : IVimHost
    {
        private IVim _vim;

        [ImportingConstructor]
        public VimCocoaHost(ITextBufferFactoryService textBufferFactoryService)
        {
            VimTrace.TraceSwitch.Level = System.Diagnostics.TraceLevel.Verbose;
            Console.WriteLine("Loaded");
        }

        public bool AutoSynchronizeSettings => false;

        public DefaultSettings DefaultSettings => DefaultSettings.GVim74;

        public string HostIdentifier => "VsVim MacOS Host";

        public bool IsAutoCommandEnabled => false;

        public bool IsUndoRedoExpected => throw new NotImplementedException();

        public int TabCount => throw new NotImplementedException();

        public bool UseDefaultCaret => true;

        public event EventHandler<TextViewEventArgs> IsVisibleChanged;
        public event EventHandler<TextViewChangedEventArgs> ActiveTextViewChanged;
        public event EventHandler<BeforeSaveEventArgs> BeforeSave;

        public void Beep()
        {
            LoggingService.LogDebug("Beep");
        }

        public void BeginBulkOperation()
        {
            
        }

        public void Close(ITextView value)
        {
            value.Close();
        }

        public void CloseAllOtherTabs(ITextView value)
        {

        }

        public void CloseAllOtherWindows(ITextView value)
        {

        }

        public ITextView CreateHiddenTextView()
        {
            throw new NotImplementedException();
        }

        public void DoActionWhenTextViewReady(FSharpFunc<Unit, Unit> action, ITextView textView)
        {
            action.Invoke(null);
        }

        public void EndBulkOperation()
        {

        }

        public void EnsurePackageLoaded()
        {
            LoggingService.LogDebug("EnsurePackageLoaded");
        }

        public void EnsureVisible(ITextView textView, SnapshotPoint point)
        {

        }

        public void FindInFiles(string pattern, bool matchCase, string filesOfType, VimGrepFlags flags, FSharpFunc<Unit, Unit> action)
        {

        }

        public void FormatLines(ITextView textView, SnapshotLineRange range)
        {

        }

        public FSharpOption<ITextView> GetFocusedTextView()
        {
            throw new NotImplementedException();
        }

        public string GetName(ITextBuffer textBuffer)
        {
            return "";
        }

        public FSharpOption<int> GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            throw new NotImplementedException();
        }

        public int GetTabIndex(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public WordWrapStyles GetWordWrapStyle(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool GoToDefinition()
        {
            throw new NotImplementedException();
        }

        public bool GoToGlobalDeclaration(ITextView textView, string identifier)
        {
            throw new NotImplementedException();
        }

        public bool GoToLocalDeclaration(ITextView textView, string identifier)
        {
            throw new NotImplementedException();
        }

        public void GoToTab(int index)
        {

        }

        public void GoToWindow(ITextView textView, WindowKind direction, int count)
        {

        }

        public bool IsDirty(ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public bool IsFocused(ITextView textView)
        {
            return true;
        }

        public bool IsReadOnly(ITextBuffer textBuffer)
        {
            return false;
        }

        public bool IsVisible(ITextView textView)
        {
            return true;
        }

        public bool LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            throw new NotImplementedException();
        }

        public FSharpOption<ITextView> LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
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
            IdeApp.Exit();
        }

        public bool Reload(ITextView textView)
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

        public void RunHostCommand(ITextView textView, string commandName, string argument)
        {
            throw new NotImplementedException();
        }

        public bool Save(ITextBuffer textBuffer)
        {
            throw new NotImplementedException();
        }

        public bool SaveTextAs(string text, string filePath)
        {
            throw new NotImplementedException();
        }

        public bool ShouldCreateVimBuffer(ITextView textView)
        {
            return true;
        }

        public bool ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            return false;
        }

        public void SplitViewHorizontally(ITextView value)
        {
            throw new NotImplementedException();
        }

        public void SplitViewVertically(ITextView value)
        {
            throw new NotImplementedException();
        }

        public void StartShell(string workingDirectory, string file, string arguments)
        {
            throw new NotImplementedException();
        }

        public bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            //throw new NotImplementedException();
            return false;
        }

        public void VimCreated(IVim vim)
        {
            _vim = vim;
        }

        public void VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            throw new NotImplementedException();
        }
    }
}
