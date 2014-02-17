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
using Microsoft.VisualStudio;
using Microsoft.FSharp.Core;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(VsVimHost))]
    [ContentType(Vim.Constants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VsVimHost : VimHost, IVsSelectionEvents
    {
        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly IVsAdapter _vsAdapter;
        private readonly ITextManager _textManager;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly _DTE _dte;
        private readonly IVsExtensibility _vsExtensibility;
        private readonly ISharedService _sharedService;
        private readonly IVsMonitorSelection _vsMonitorSelection;
        private readonly IFontProperties _fontProperties;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        /// <summary>
        /// Should we create IVimBuffer instances for new ITextView values
        /// </summary>
        public bool DisableVimBufferCreation
        { 
            get; 
            set; 
        }

        /// <summary>
        /// Don't automatically synchronize settings.  Visual Studio applies settings at uncertain times and hence this
        /// behavior must be special cased.  It is handled by HostFactory
        /// </summary>
        public override bool AutoSynchronizeSettings
        {
            get { return false; }
        }

        public override int TabCount
        {
            get { return _sharedService.GetWindowFrameState().WindowFrameCount; }
        }

        public override IFontProperties FontProperties
        {
            get { return _fontProperties; }
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
            ITextManager textManager,
            ISharedServiceFactory sharedServiceFactory,
            SVsServiceProvider serviceProvider)
            : base(textBufferFactoryService, textEditorFactoryService, textDocumentFactoryService, editorOperationsFactoryService)
        {
            _vsAdapter = adapter;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _vsExtensibility = (IVsExtensibility)serviceProvider.GetService(typeof(IVsExtensibility));
            _textManager = textManager;
            _sharedService = sharedServiceFactory.Create();
            _vsMonitorSelection = serviceProvider.GetService<SVsShellMonitorSelection, IVsMonitorSelection>();
            _fontProperties = new TextEditorFontProperties(serviceProvider);

            uint cookie;
            _vsMonitorSelection.AdviseSelectionEvents(this, out cookie);
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
        /// Get the C++ identifier which exists under the caret 
        /// </summary>
        private static string GetCPlusPlusIdentifier(ITextView textView)
        {
            var snapshot = textView.TextSnapshot;
            Func<int, bool> isValid = (position) => 
            {
                if (position < 0 || position >= snapshot.Length)
                {
                    return false;
                }

                var c = snapshot[position];
                return Char.IsLetter(c) || Char.IsDigit(c) || c == '_';
            };

            var start = textView.Caret.Position.BufferPosition.Position;
            if (!isValid(start))
            {
                return null;
            }

            var end = start + 1;
            while (isValid(end))
            {
                end++;
            }

            while (isValid(start - 1))
            {
                start--;
            }

            var span = new SnapshotSpan(snapshot, start, end - start);
            return span.GetText();
        }

        /// <summary>
        /// The C++ project system requires that the target of GoToDefinition be passed
        /// as an argument to the command.  
        /// </summary>
        private bool GoToDefinitionCPlusPlus(ITextView textView, string target)
        {
            if (target == null)
            {
                target = GetCPlusPlusIdentifier(textView);
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
            var selected = _textManager
                .GetDocumentTextViews(DocumentLoad.RespectLazy)
                .Where(x => !x.Selection.IsEmpty).ToList();
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
            _textManager
                .GetDocumentTextViews(DocumentLoad.RespectLazy)
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

        public override int GetTabIndex(ITextView textView)
        {
            // TODO: Should look for the actual index instead of assuming this is called on the 
            // active ITextView.  They may not actually be equal
            var windowFrameState = _sharedService.GetWindowFrameState();
            return windowFrameState.ActiveWindowFrameIndex;
        }

        public override void GoToTab(int index)
        {
            _sharedService.GoToTab(index);
        }

        public override bool GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
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

            return true;
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

        public override HostResult MoveFocus(ITextView textView, Direction direction)
        {
            bool result = false;
            switch (direction)
            {
                case Direction.Up:
                    result = _textManager.MoveViewUp(textView);
                    break;
                case Direction.Down:
                    result = _textManager.MoveViewDown(textView);
                    break;
            }

            return result ? HostResult.Success : HostResult.NewError("Not Implemented");
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

        public override bool ShouldCreateVimBuffer(ITextView textView)
        {
            if (textView.Roles.Contains(Constants.TextViewRoleEmbeddedPeekTextView))
            {
                return true;
            }

            if (!base.ShouldCreateVimBuffer(textView))
            {
                return false;
            }

            return !DisableVimBufferCreation;
        }

        #region IVsSelectionEvents

        int IVsSelectionEvents.OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            var id = (VSConstants.VSSELELEMID)elementid;
            if (id == VSConstants.VSSELELEMID.SEID_WindowFrame)
            {
                Func<object, ITextView> getTextView =
                    obj =>
                    {
                        var vsWindowFrame = obj as IVsWindowFrame;
                        if (vsWindowFrame == null)
                        {
                            return null;
                        }

                        var vsCodeWindow = vsWindowFrame.GetCodeWindow();
                        if (vsCodeWindow.IsError)
                        {
                            return null;
                        }

                        var lastActiveTextView = vsCodeWindow.Value.GetLastActiveView(_vsAdapter.EditorAdapter);
                        if (lastActiveTextView.IsError)
                        {
                            return null;
                        }

                        return lastActiveTextView.Value;
                    };

                ITextView oldView = getTextView(varValueOld);
                ITextView newView = null;
                object value;
                if (ErrorHandler.Succeeded(_vsMonitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out value)))
                {
                    newView = getTextView(value);
                }

                RaiseActiveTextViewChanged(
                    oldView == null ? FSharpOption<ITextView>.None : FSharpOption.Create<ITextView>(oldView),
                    newView == null ? FSharpOption<ITextView>.None : FSharpOption.Create<ITextView>(newView));
            }

            return VSConstants.S_OK;
        }

        int IVsSelectionEvents.OnSelectionChanged(IVsHierarchy pHierOld, uint itemidOld, IVsMultiItemSelect pMISOld, ISelectionContainer pSCOld, IVsHierarchy pHierNew, uint itemidNew, IVsMultiItemSelect pMISNew, ISelectionContainer pSCNew)
        {
            return VSConstants.S_OK;
        }

        #endregion
    }
}
