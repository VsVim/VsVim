using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using AppKit;
using Foundation;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.CodeFormatting;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Projects;
using Vim.Extensions;
using Vim.Interpreter;
using Export = System.ComponentModel.Composition.ExportAttribute ;

namespace Vim.Mac
{
    [Export(typeof(IVimHost))]
    [Export(typeof(VimCocoaHost))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VimCocoaHost : IVimHost
    {
        private readonly ITextBufferFactoryService _textBufferFactoryService;
        private readonly ICocoaTextEditorFactoryService _textEditorFactoryService;
        private readonly ISmartIndentationService _smartIndentationService;
        private IVim _vim;

        internal const string CommandNameGoToDefinition = "MonoDevelop.Refactoring.RefactoryCommands.GotoDeclaration";

        [ImportingConstructor]
        public VimCocoaHost(
            ITextBufferFactoryService textBufferFactoryService,
            ICocoaTextEditorFactoryService textEditorFactoryService,
            ISmartIndentationService smartIndentationService)
        {
            VimTrace.TraceSwitch.Level = System.Diagnostics.TraceLevel.Verbose;
            Console.WriteLine("Loaded");
            _textBufferFactoryService = textBufferFactoryService;
            _textEditorFactoryService = textEditorFactoryService;
            _smartIndentationService = smartIndentationService;
        }

        public bool AutoSynchronizeSettings => false;

        public DefaultSettings DefaultSettings => DefaultSettings.GVim74;

        public string HostIdentifier => "VsVim MacOS Host";

        public bool IsAutoCommandEnabled => false;

        public bool IsUndoRedoExpected => throw new NotImplementedException();

        public int TabCount => IdeApp.Workbench.Documents.Count;

        public bool UseDefaultCaret => true;

        public event EventHandler<TextViewEventArgs> IsVisibleChanged;
        public event EventHandler<TextViewChangedEventArgs> ActiveTextViewChanged;
        public event EventHandler<BeforeSaveEventArgs> BeforeSave;

        private ITextView TextViewFromDocument(Document document)
        {
            return document.GetContent<ITextView>();
        }

        private Document DocumentFromTextView(ITextView textView)
        {
            return IdeApp.Workbench.Documents.FirstOrDefault(doc => TextViewFromDocument(doc) == textView);
        }

        private Document DocumentFromTextBuffer(ITextBuffer textBuffer)
        {
            return IdeApp.Workbench.Documents.FirstOrDefault(doc => doc.TextBuffer == textBuffer);
        }

        private static NSSound GetBeepSound()
        {
            using (var stream = typeof(VimCocoaHost).Assembly.GetManifestResourceStream("Vim.Mac.Resources.beep.wav"))
            {
                NSData data = NSData.FromStream(stream);
                return new NSSound(data);
            }
        }

        readonly Lazy<NSSound> beep = new Lazy<NSSound>(() => GetBeepSound());

        public void Beep()
        {
            beep.Value.Play();
        }

        public void BeginBulkOperation()
        {

        }

        public void Close(ITextView textView)
        {
            Dispatch(FileCommands.CloseFile);
        }

        public void CloseAllOtherTabs(ITextView textView)
        {
            Dispatch(FileTabCommands.CloseAllButThis);
        }

        public void CloseAllOtherWindows(ITextView textView)
        {
            Dispatch(FileTabCommands.CloseAllButThis);
        }

        /// <summary>
        /// Create a hidden ITextView.  It will have no roles in order to keep it out of 
        /// most plugins
        /// </summary>
        public ITextView CreateHiddenTextView()
        {
            return _textEditorFactoryService.CreateTextView(
                _textBufferFactoryService.CreateTextBuffer(),
                _textEditorFactoryService.NoRoles);
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

        // TODO: Same as WPF version
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

        public void FindInFiles(string pattern, bool matchCase, string filesOfType, VimGrepFlags flags, FSharpFunc<Unit, Unit> action)
        {

        }

        public void FormatLines(ITextView textView, SnapshotLineRange range)
        {
            var startedWithSelection = !textView.Selection.IsEmpty;
            textView.Selection.Clear();
            textView.Selection.Select(range.ExtentIncludingLineBreak, false);

            // FormatBuffer command actually formats the selection
            Dispatch(CodeFormattingCommands.FormatBuffer);
            if (!startedWithSelection)
            {
                textView.Selection.Clear();
            }
        }

        public FSharpOption<ITextView> GetFocusedTextView()
        {
            var doc = IdeServices.DocumentManager.ActiveDocument;
            return FSharpOption.CreateForReference(TextViewFromDocument(doc));
        }

        public string GetName(ITextBuffer textBuffer)
        {
            if (textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDocument) && textDocument.FilePath != null)
            {
                return textDocument.FilePath;
            }
            else
            {
                LoggingService.LogWarning("VsVim: Failed to get filename of textbuffer.");
                return "";
            }
        }

        //TODO: Copied from VsVimHost
        public FSharpOption<int> GetNewLineIndent(ITextView textView, ITextSnapshotLine contextLine, ITextSnapshotLine newLine, IVimLocalSettings localSettings)
        {
            //if (_vimApplicationSettings.UseEditorIndent)
            //{
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
                // is enabled.  Do a best effort and use Vim style indenting
                return FSharpOption.Create(EditUtil.GetAutoIndent(contextLine, localSettings.TabStop));
            }
            //}

            //return FSharpOption<int>.None;
        }

        public int GetTabIndex(ITextView textView)
        {
            var notebooks = WindowManagement.GetNotebooks();
            foreach (var notebook in notebooks)
            {
                var index = notebook.FileNames.IndexOf(GetName(textView.TextBuffer));
                if (index != -1)
                {
                    return index;
                }
            }
            return -1;
        }

        public WordWrapStyles GetWordWrapStyle(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public bool GoToDefinition()
        {
            return Dispatch(CommandNameGoToDefinition);
        }

        public bool GoToGlobalDeclaration(ITextView textView, string identifier)
        {
            return Dispatch(CommandNameGoToDefinition);
        }

        public bool GoToLocalDeclaration(ITextView textView, string identifier)
        {
            return Dispatch(CommandNameGoToDefinition);
        }

        private void OpenTab(string fileName)
        {
            Project project = null;
            IdeApp.Workbench.OpenDocument(fileName, project).Wait(System.Threading.CancellationToken.None);
        }

        public void GoToTab(int index)
        {
            var activeNotebook = WindowManagement.GetNotebooks().First(n => n.IsActive);
            var fileName = activeNotebook.FileNames[index];
            OpenTab(fileName);
        }

        private void SwitchToNotebook(Notebook notebook)
        {
            OpenTab(notebook.FileNames[notebook.ActiveTab]);
        }

        public void GoToWindow(ITextView textView, WindowKind direction, int count)
        {
            // In VSMac, there are just 2 windows, left and right
            var notebooks = WindowManagement.GetNotebooks();

            if (notebooks.Length > 0 && notebooks[0].IsActive && (direction == WindowKind.Right || direction == WindowKind.Previous || direction == WindowKind.Next))
            {
                SwitchToNotebook(notebooks[1]);
            }

            if (notebooks.Length > 0 && notebooks[1].IsActive && (direction == WindowKind.Left || direction == WindowKind.Previous || direction == WindowKind.Next))
            {
                SwitchToNotebook(notebooks[0]);
            }
        }

        public bool IsDirty(ITextBuffer textBuffer)
        {
            var doc = DocumentFromTextBuffer(textBuffer);
            return doc.IsDirty;
        }

        public bool IsFocused(ITextView textView)
        {
            return TextViewFromDocument(IdeServices.DocumentManager.ActiveDocument) == textView;
        }

        public bool IsReadOnly(ITextBuffer textBuffer)
        {
            var doc = DocumentFromTextBuffer(textBuffer);
            return doc.IsViewOnly;
        }

        public bool IsVisible(ITextView textView)
        {
            return IdeServices.DocumentManager.Documents.Select(TextViewFromDocument).Any(v => v == textView);
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
            Dispatch(ProjectCommands.Build);
        }

        public bool NavigateTo(VirtualSnapshotPoint point)
        {
            var tuple = SnapshotPointUtil.GetLineNumberAndOffset(point.Position);
            var line = tuple.Item1;
            var column = tuple.Item2;
            var buffer = point.Position.Snapshot.TextBuffer;
            var fileName = GetName(buffer);

            try
            {
                IdeApp.Workbench.OpenDocument(fileName, null, line, column).Wait(System.Threading.CancellationToken.None);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public FSharpOption<ListItem> NavigateToListItem(ListKind listKind, NavigationKind navigationKind, FSharpOption<int> argument, bool hasBang)
        {
        //    public enum ListKind
        //{
        //    Error,
        //    Location
        //}
        //public enum NavigationKind
        //{
        //    First,
        //    Last,
        //    Next,
        //    Previous
        //}
            throw new NotImplementedException();
        }

        public bool OpenLink(string link)
        {
            return NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(link));
        }

        public void OpenListWindow(ListKind listKind)
        {
            if (listKind == ListKind.Error)
            {
                GotoPad("MonoDevelop.Ide.Gui.Pads.ErrorListPad");
                return;
            }

            if (listKind == ListKind.Location)
            {
                // This abstraction is not quite right as VSMac can have multiple search results pads open
                GotoPad("SearchPad - Search Results - 0");
                return;
            }

        }

        private void GotoPad(string padId)
        {
            var pad = IdeApp.Workbench.Pads.FirstOrDefault(p => p.Id == padId);
            pad?.BringToFront(true);
        }
        
        public void Quit()
        {
            IdeApp.Exit();
        }

        public bool Reload(ITextView textView)
        {
            var doc = DocumentFromTextView(textView);
            doc.Reload();
            return true;
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
            Dispatch(commandName);
        }

        public bool Save(ITextBuffer textBuffer)
        {
            var doc = DocumentFromTextBuffer(textBuffer);
            try
            {
                doc.Save();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool SaveTextAs(string text, string filePath)
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

        public bool ShouldCreateVimBuffer(ITextView textView)
        {
            return true;
        }

        public bool ShouldIncludeRcFile(VimRcPath vimRcPath)
        {
            return File.Exists(vimRcPath.FilePath);
        }

        public void SplitViewHorizontally(ITextView value)
        {
            Dispatch("MonoDevelop.Ide.Commands.ViewCommands.SideBySideMode");
        }

        public void SplitViewVertically(ITextView value)
        {
            Dispatch("MonoDevelop.Ide.Commands.ViewCommands.SideBySideMode");
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
            //throw new NotImplementedException();
        }

        bool Dispatch(object command)
        {
            try
            {
                return IdeApp.CommandService.DispatchCommand(command);
            }
            catch
            {
                return false;
            }
        }
    }
}
