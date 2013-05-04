using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim;

namespace VimApp
{
    [Export(typeof(IVimHost))]
    [Export(typeof(DefaultVimHost))]
    sealed class DefaultVimHost : Vim.UI.Wpf.VimHost
    {
        internal MainWindow MainWindow
        {
            get;
            set;
        }

        [ImportingConstructor]
        internal DefaultVimHost(
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

        public override void FormatLines(ITextView textView, EditorUtils.SnapshotLineRange range)
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

        public override HostResult LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            return HostResult.NewError("");
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

        public override void ShowOpenFileDialog()
        {

        }

        public override HostResult SplitViewHorizontally(ITextView value)
        {
            MainWindow.SplitViewHorizontally(value);
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
