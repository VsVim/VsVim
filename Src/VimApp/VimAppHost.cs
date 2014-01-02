using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim;
using EditorUtils;
using System;
using System.Windows.Input;

namespace VimApp
{
    [Export(typeof(IVimHost))]
    [Export(typeof(VimAppHost))]
    internal sealed class VimAppHost : Vim.UI.Wpf.VimHost
    {
        private sealed class TextEditorFontProperties : IFontProperties
        {
            public System.Windows.Media.FontFamily FontFamily
            {
                get { return Constants.FontFamily; }
            }

            public double FontSize
            {
                get { return Constants.FontSize; }
            }

            public event EventHandler<FontPropertiesEventArgs> FontPropertiesChanged;

            internal void OnFontPropertiesChanged()
            {
                var handler = FontPropertiesChanged;
                if (handler != null)
                {
                    handler(this, new FontPropertiesEventArgs());
                }
            }
        }

        private const string ErrorCouldNotFindVimViewInfo = "Could not find the associated IVimViewInfo";
        private const string ErrorUnsupported = "Could not find the associated IVimViewInfo";
        private const string ErrorInvalidDirection = "Invalid direction";

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

        public override int TabCount
        {
            get { return MainWindow.TabControl.Items.Count; }
        }

        public override IFontProperties FontProperties
        {
            get { return new TextEditorFontProperties(); }
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

        public override int GetTabIndex(ITextView textView)
        {
            IVimViewInfo vimViewInfo;
            if (!TryGetVimViewInfo(textView, out vimViewInfo))
            {
                return -1;
            }

            return MainWindow.TabControl.Items.IndexOf(vimViewInfo.VimWindow.TabItem);
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

        public override void GoToTab(int index)
        {
            MainWindow.TabControl.SelectedIndex = index;
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
            try
            {
                var textDocument = TextDocumentFactoryService.CreateAndLoadTextDocument(filePath, TextBufferFactoryService.TextContentType);
                var wpfTextView = MainWindow.CreateTextView(textDocument.TextBuffer);
                MainWindow.AddNewTab(System.IO.Path.GetFileName(filePath), wpfTextView);
                return HostResult.Success;
            }
            catch (Exception ex)
            {
                return HostResult.NewError(ex.Message);
            }
        }

        public override HostResult Make(bool jumpToFirstError, string arguments)
        {
            return HostResult.NewError(ErrorUnsupported);
        }

        public override HostResult MoveFocus(ITextView textView, Direction direction)
        {
            foreach (var vimWindow in _vimWindowManager.VimWindowList)
            {
                var list = vimWindow.VimViewInfoList;
                int i = 0;
                while (i < list.Count)
                {
                    if (list[i].TextView == textView)
                    {
                        break;
                    }

                    i++;
                }

                if (i >= list.Count)
                {
                    continue;
                }

                int target = -1;
                switch (direction)
                {
                    case Direction.Up:
                        target = i - 1;
                        break;
                    case Direction.Down:
                        target = i + 1;
                        break;
                }

                if (target >= 0 && target < list.Count)
                {
                    var targetTextView = list[target].TextViewHost.TextView;
                    Keyboard.Focus(targetTextView.VisualElement);
                    return HostResult.Success;
                }
                else
                {
                    return HostResult.NewError(ErrorInvalidDirection);
                }
            }

            return HostResult.NewError(ErrorCouldNotFindVimViewInfo);
        }

        public override bool NavigateTo(VirtualSnapshotPoint point)
        {
            return false;
        }

        public override void RunVisualStudioCommand(string command, string argument)
        {

        }

        public override HostResult SplitViewHorizontally(ITextView textView)
        {
            // First find the IVimViewInfo that contains this ITextView
            IVimViewInfo vimViewInfo;
            if (!TryGetVimViewInfo(textView, out vimViewInfo))
            {
                return HostResult.NewError(ErrorCouldNotFindVimViewInfo);
            }

            var newTextView = MainWindow.CreateTextView(textView.TextBuffer);
            var newTextViewHost = MainWindow.CreateTextViewHost(newTextView);
            vimViewInfo.VimWindow.AddVimViewInfo(newTextViewHost);
            return HostResult.Success;
        }

        public override HostResult SplitViewVertically(ITextView value)
        {
            return HostResult.NewError(ErrorUnsupported);
        }

        public override bool GoToQuickFix(QuickFix quickFix, int count, bool hasBang)
        {
            return false;
        }

        private bool TryGetVimViewInfo(ITextView textView, out IVimViewInfo vimViewInfo)
        {
            vimViewInfo = _vimWindowManager.VimWindowList
                .SelectMany(x => x.VimViewInfoList)
                .Where(x => x.TextViewHost.TextView == textView)
                .FirstOrDefault();
            return vimViewInfo != null;
        }
    }
}
