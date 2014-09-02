using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim;
using Vim.Extensions;
using EditorUtils;
using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Utilities;
using Vim.UI.Wpf;
using System.Windows.Threading;

namespace VimApp
{
    [Export(typeof(IVimHost))]
    [Export(typeof(VimAppHost))]
    internal sealed class VimAppHost : Vim.UI.Wpf.VimHost
    {
        private const string ErrorCouldNotFindVimViewInfo = "Could not find the associated IVimViewInfo";
        private const string ErrorUnsupported = "Could not find the associated IVimViewInfo";
        private const string ErrorInvalidDirection = "Invalid direction";

        private readonly IFileSystem _fileSystem;
        private readonly IDirectoryUtil _directoryUtil;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private IVimWindowManager _vimWindowManager;
        private IVim _vim;

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

        [ImportingConstructor]
        internal VimAppHost(
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            IFileSystem fileSystem,
            IDirectoryUtil directoryUtil) : base(
            textBufferFactoryService,
            textEditorFactoryService,
            textDocumentFactoryService,
            editorOperationsFactoryService)
        {
            _contentTypeRegistryService = contentTypeRegistryService;
            _fileSystem = fileSystem;
            _directoryUtil = directoryUtil;
        }

        public override void VimCreated(IVim vim)
        {
            _vim = vim;
            _vim.VimData.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
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
        public override bool LoadFileIntoExistingWindow(string filePath, ITextView textView)
        {
            var vimWindow = MainWindow.ActiveVimWindowOpt;
            if (vimWindow == null)
            {
                _vim.ActiveStatusUtil.OnError("No active vim window");
                return false;
            }

            IWpfTextView createdTextView;
            if (TryLoadPath(filePath, out createdTextView))
            {
                var wpfTextViewHost = MainWindow.CreateTextViewHost(createdTextView);
                vimWindow.Clear();
                vimWindow.AddVimViewInfo(wpfTextViewHost);
                Dispatcher.CurrentDispatcher.BeginInvoke(
                    (Action)(() =>
                    {
                        var control = wpfTextViewHost.TextView.VisualElement;
                        control.IsEnabled = true;
                        control.Focusable = true;
                        control.Focus();
                    }),
                    DispatcherPriority.ApplicationIdle);

                return true;
            }
            else
            {
                _vim.ActiveStatusUtil.OnError("Could not load file");
                return false;
            }
        }

        public override bool LoadFileIntoNewWindow(string filePath)
        {
            try
            {
                var textDocument = TextDocumentFactoryService.CreateAndLoadTextDocument(filePath, TextBufferFactoryService.TextContentType);
                var wpfTextView = MainWindow.CreateTextView(textDocument.TextBuffer);
                MainWindow.AddNewTab(System.IO.Path.GetFileName(filePath), wpfTextView);
                return true;
            }
            catch (Exception ex)
            {
                _vim.ActiveStatusUtil.OnError(ex.Message);
                return false;
            }
        }

        public override void Make(bool jumpToFirstError, string arguments)
        {
            _vim.ActiveStatusUtil.OnError(ErrorUnsupported);
        }

        public override void MoveFocus(ITextView textView, Direction direction)
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
                }
                else
                {
                    _vim.ActiveStatusUtil.OnError(ErrorInvalidDirection);
                }
            }

            _vim.ActiveStatusUtil.OnError(ErrorCouldNotFindVimViewInfo);
        }

        public override bool NavigateTo(VirtualSnapshotPoint point)
        {
            return false;
        }

        public override void RunVisualStudioCommand(ITextView textView, string command, string argument)
        {

        }

        public override void SplitViewHorizontally(ITextView textView)
        {
            // First find the IVimViewInfo that contains this ITextView
            IVimViewInfo vimViewInfo;
            if (!TryGetVimViewInfo(textView, out vimViewInfo))
            {
                _vim.ActiveStatusUtil.OnError(ErrorCouldNotFindVimViewInfo);
                return;
            }

            var newTextView = MainWindow.CreateTextView(textView.TextBuffer);
            var newTextViewHost = MainWindow.CreateTextViewHost(newTextView);
            vimViewInfo.VimWindow.AddVimViewInfo(newTextViewHost);
        }

        public override void SplitViewVertically(ITextView value)
        {
            _vim.ActiveStatusUtil.OnError(ErrorUnsupported);
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

        private bool TryLoadPath(string filePath, out IWpfTextView textView)
        {
            return 
                TryLoadPathAsFile(filePath, out textView) ||
                TryLoadPathAsDirectory(filePath, out textView);
        }

        private bool TryLoadPathAsFile(string filePath, out IWpfTextView textView)
        {
            try
            {
                var textDocument = TextDocumentFactoryService.CreateAndLoadTextDocument(filePath, TextBufferFactoryService.TextContentType);
                textView = MainWindow.CreateTextView(textDocument.TextBuffer);
                return true;
            }
            catch (Exception)
            {
                textView = null;
                return false;
            }
        }

        private bool TryLoadPathAsDirectory(string filePath, out IWpfTextView textView)
        {
            ITextBuffer textBuffer;
            if (!_directoryUtil.TryCreateDirectoryTextBuffer(filePath, out textBuffer))
            {
                textView = null;
                return false;
            }

            textView = MainWindow.CreateTextView(textBuffer, PredefinedTextViewRoles.Interactive);
            return true;
        }
    }
}
