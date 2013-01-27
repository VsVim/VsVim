using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using EditorUtils;
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

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class VsVimHost : VimHost
    {
        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly IVsAdapter _vsAdapter;
        private readonly ITextManager _textManager;
        private readonly IWordUtilFactory _wordUtilFactory;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly _DTE _dte;
        private readonly IVsExtensibility _vsExtensibility;
        private readonly ISharedService _sharedService;

        internal _DTE DTE
        {
            get { return _dte; }
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
            IWordUtilFactory wordUtilFactory,
            ITextManager textManager,
            ISharedServiceFactory sharedServiceFactory,
            SVsServiceProvider serviceProvider)
            : base(textBufferFactoryService, textEditorFactoryService, textDocumentFactoryService, editorOperationsFactoryService)
        {
            _vsAdapter = adapter;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _wordUtilFactory = wordUtilFactory;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _vsExtensibility = (IVsExtensibility)serviceProvider.GetService(typeof(IVsExtensibility));
            _textManager = textManager;
            _sharedService = sharedServiceFactory.Create();
        }

        private bool SafeExecuteCommand(string command, string args = "")
        {
            try
            {
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// The C++ project system requires that the target of GoToDefinition be passed
        /// as an argument to the command.  
        /// </summary>
        private bool GoToDefinitionCPlusPlus(ITextView textView, string target)
        {
            if (target == null)
            {
                var caretPoint = textView.Caret.Position.BufferPosition;
                var wordUtil = _wordUtilFactory.GetWordUtil(textView.TextBuffer);
                var span = wordUtil.GetFullWordSpan(WordKind.NormalWord, caretPoint);
                target = span.IsSome()
                    ? span.Value.GetText()
                    : null;
            }

            if (target != null)
            {
                return SafeExecuteCommand(CommandNameGoToDefinition, target);
            }

            return SafeExecuteCommand(CommandNameGoToDefinition);
        }

        private bool GoToDefinitionCore(ITextView textView, string target)
        {
            if (textView.TextBuffer.ContentType.IsCPlusPlus())
            {
                return GoToDefinitionCPlusPlus(textView, target);
            }

            return SafeExecuteCommand(CommandNameGoToDefinition);
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
            SafeExecuteCommand("Edit.FormatSelection");
            if (!startedWithSelection)
            {
                textView.Selection.Clear();
            }
        }

        public override bool GoToDefinition()
        {
            var selected = _textManager.TextViews.Where(x => !x.Selection.IsEmpty).ToList();
            if (!GoToDefinitionCore(_textManager.ActiveTextViewOptional, null))
            {
                return false;
            }

            // Certian language services, VB.Net for example, will select the word after
            // the go to definition is implemented.  Need to clear that out to prevent the
            // go to definition from switching us to Visual Mode
            // 
            // This selection often occurs in another document but that document won't be 
            // active when we get back here.  Instead just clear all of the new selections
            _textManager.TextViews
                .Where(x => !x.Selection.IsEmpty && !selected.Contains(x))
                .ForEach(x => x.Selection.Clear());

            return true;
        }

        /// <summary>
        /// In a perfect world this would replace the contents of the existing ITextView
        /// with those of the specified file.  Unfortunately this causes problems in 
        /// Visual Studio when the file is of a different content type.  Instead we 
        /// mimic the behavior by opening the document in a new window and closing the
        /// existing one
        /// </summary>
        public override HostResult LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            try
            {
                // Open the document before closing the other.  That way any error which occurs
                // during an open will cause the method to abandon and produce a user error 
                // message
                VsShellUtilities.OpenDocument(_vsAdapter.ServiceProvider, filePath);
                _textManager.CloseView(textView);
                return HostResult.Success;
            }
            catch (Exception e)
            {
                return HostResult.NewError(e.Message);
            }
        }

        /// <summary>
        /// Open up a new document window with the specified file
        /// </summary>
        public override HostResult LoadFileIntoNewWindow(string filePath)
        {
            try
            {
                VsShellUtilities.OpenDocument(_vsAdapter.ServiceProvider, filePath);
                return HostResult.Success;
            }
            catch (Exception e)
            {
                return HostResult.NewError(e.Message);
            }
        }

        public override void ShowOpenFileDialog()
        {
            SafeExecuteCommand("Edit.OpenFile");
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
                return String.Empty;
            }
            return vsTextLines.GetFileName();
        }

        public override bool Save(ITextBuffer textBuffer)
        {
            return _textManager.Save(textBuffer).IsSuccess;
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

        /// <summary>
        /// Custom process the insert command if possible.  This is handled by VsCommandTarget
        /// </summary>
        public override bool TryCustomProcess(ITextView textView, InsertCommand command)
        {
            VsCommandTarget vsCommandTarget;
            if (VsCommandTarget.TryGet(textView, out vsCommandTarget))
            {
                return vsCommandTarget.TryCustomProcess(command);
            }

            return false;
        }

        /// <summary>
        /// Go to the 'count' tab in the given direction.  If the count exceeds the count in
        /// the given direction then it should wrap around to the end of the list of items
        /// </summary>
        public override void GoToNextTab(Vim.Path direction, int count)
        {
            // First get the index of the current tab so we know where we are incrementing
            // from.  Make sure to check that our view is actually a part of the active
            // views
            var windowFrameState = _sharedService.GetWindowFrameState();
            var index = windowFrameState.ActiveWindowFrameIndex;
            if (index == -1)
            {
                return;
            }

            var childCount = windowFrameState.WindowFrameCount;
            count = count % childCount;
            if (direction.IsForward)
            {
                index += count;
                index %= childCount;
            }
            else
            {
                index -= count;
                if (index < 0)
                {
                    index += childCount;
                }
            }

            _sharedService.GoToTab(index);
        }

        public override void GoToTab(int index)
        {
            var windowFrameState = _sharedService.GetWindowFrameState();
            var realIndex = -1;
            if (index < 0)
            {
                realIndex = windowFrameState.WindowFrameCount - 1;
            }
            else if (index == 0)
            {
                realIndex = 0;
            }
            else
            {
                realIndex = index - 1;
            }

            if (realIndex >= 0 && realIndex < windowFrameState.WindowFrameCount)
            {
                _sharedService.GoToTab(realIndex);
            }
        }

        public override void GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            // This implementation could be much more riguorous but for next a simple navigation
            // of the next and previous error will suffice
            var command = quickFix.IsNext
                ? "View.NextError"
                : "View.PreviousError";
            for (var i = 0; i < count; i++)
            {
                SafeExecuteCommand(command);
            }
        }

        public override HostResult Make(bool jumpToFirstError, string arguments)
        {
            SafeExecuteCommand("Build.BuildSolution");
            return HostResult.Success;
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

        public override void RunVisualStudioCommand(string command, string argument)
        {
            SafeExecuteCommand(command, argument);
        }

        /// <summary>
        /// Perform a horizontal window split 
        /// </summary>
        public override HostResult SplitViewHorizontally(ITextView textView)
        {
            _textManager.SplitView(textView);
            return HostResult.Success;
        }

        /// <summary>
        /// Perform a vertical buffer split, which is essentially just another window in a different tab group.
        /// </summary>
        public override HostResult SplitViewVertically(ITextView value)
        {
            try
            {
                _dte.ExecuteCommand("Window.NewWindow");
                _dte.ExecuteCommand("Window.NewVerticalTabGroup");
                return HostResult.Success;
            }
            catch (Exception e)
            {
                return HostResult.NewError(e.Message);
            }
        }

        public override void MoveViewDown(ITextView textView)
        {
            _textManager.MoveViewDown(textView);
        }

        public override void MoveViewUp(ITextView textView)
        {
            _textManager.MoveViewUp(textView);
        }

        /// <summary>
        /// Not yet implemented!
        /// </summary>
        public override void MoveViewLeft(ITextView value)
        {
            // Not yet implemented!
        }

        /// <summary>
        /// Not yet implemented!
        /// </summary>
        public override void MoveViewRight(ITextView value)
        {
            // Not yet implemented!
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

        public override void VimRcLoaded(VimRcState vimRcState, IVimLocalSettings localSettings, IVimWindowSettings windowSettings)
        {
            if (vimRcState.IsLoadFailed)
            {
                // If we failed to load a vimrc file then we should add a couple of sanity 
                // settings.  Otherwise the Visual Studio experience wont't be what users expect
                localSettings.AutoIndent = true;
            }
        }
    }
}
