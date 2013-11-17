using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim;
using EditorUtils;
using System;

namespace VimApp
{
    [Export(typeof(IVimHost))]
    [Export(typeof(VimAppHost))]
    internal sealed class VimAppHost : Vim.UI.Wpf.VimHost
    {
        private IVimWindowManager _vimWindowManager;

        internal IVimWindowManager VimWindowManager
        {
            get { return _vimWindowManager; }
            set { _vimWindowManager = value; }
        }

        internal MainWindow MainWindow
        {
            get;
            set;
        }

        [ImportingConstructor]
        internal VimAppHost(
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService) : base(
            textBufferFactoryService,
            textEditorFactoryService,
            textDocumentFactoryService,
            editorOperationsFactoryService)
        {

        }

        public override void FormatLines(ITextView textView, SnapshotLineRange range)
        {

        }

        public override string GetName(ITextBuffer value)
        {
            return "";
        }

        public override bool GoToDefinition()
        {
            return false;
        }

        public override bool GoToGlobalDeclaration(ITextView textView, string name)
        {
            return false;
        }

        public override bool GoToLocalDeclaration(ITextView textView, string name)
        {
            return false;
        }

        public override void GoToNextTab(Path direction, int count)
        {

        }

        public override void GoToTab(int index)
        {

        }

        // TODO: The ITextView parameter isn't necessary.  This command should always load into
        // the active window, not existing
        public override HostResult LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            var vimWindow = MainWindow.ActiveVimWindowOpt;
            if (vimWindow == null)
            {
                return HostResult.NewError("No active vim window");
            }

            try
            {
                var textDocument = TextDocumentFactoryService.CreateAndLoadTextDocument(filePath, TextBufferFactoryService.TextContentType);
                var wpfTextViewHost = MainWindow.CreateTextViewHost(MainWindow.CreateTextView(textDocument.TextBuffer));
                vimWindow.Clear();
                vimWindow.AddVimViewInfo(wpfTextViewHost);
                return HostResult.Success;
            }
            catch (Exception ex)
            {
                return HostResult.NewError(ex.Message);
            }
        }

        public override HostResult LoadFileIntoNewWindow(string filePath)
        {
            return HostResult.NewError("");
        }

        public override HostResult Make(bool jumpToFirstError, string arguments)
        {
            return HostResult.NewError("");
        }

        public override HostResult MoveFocus(ITextView textView, Direction direction)
        {
            return HostResult.NewError("Not Supported");
        }

        public override bool NavigateTo(VirtualSnapshotPoint point)
        {
            return false;
        }

        public override void RunVisualStudioCommand(string command, string argument)
        {

        }

        public override HostResult SplitViewHorizontally(ITextView value)
        {
            MainWindow.SplitViewHorizontally((IWpfTextView)value);
            return HostResult.Success;
        }

        public override HostResult SplitViewVertically(ITextView value)
        {
            return HostResult.NewError("");
        }

        public override void GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            throw new System.NotImplementedException();
        }
    }
}
