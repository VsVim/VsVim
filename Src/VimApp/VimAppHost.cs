using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Vim;
using Vim.EditorHost;
using System;
using System.Windows.Input;
using Microsoft.VisualStudio.Utilities;
using Vim.UI.Wpf;
using System.Windows.Threading;
using Microsoft.FSharp.Core;
using Vim.Extensions;
using Vim.Interpreter;
using Vim.VisualStudio.Specific;
using System.Collections.Generic;

namespace VimApp
{
    [Export(typeof(IVimHost))]
    [Export(typeof(IWpfTextViewCreationListener))]
    [Export(typeof(VimAppHost))]
    [ContentType(VimConstants.ContentType)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class VimAppHost : Vim.UI.Wpf.VimHost
    {
        private const string ErrorCouldNotFindVimViewInfo = "Could not find the associated IVimViewInfo";
        private const string ErrorUnsupported = "Could not find the associated IVimViewInfo";
        private const string ErrorInvalidDirection = "Invalid direction";

        private readonly IProtectedOperations _protectedOperations;
        private readonly IFileSystem _fileSystem;
        private readonly IDirectoryUtil _directoryUtil;
        private readonly IContentTypeRegistryService _contentTypeRegistryService;
        private IVimWindowManager _vimWindowManager;
        private IVim _vim;

        public override string HostIdentifier => VimSpecificUtil.HostIdentifier;

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
            IProtectedOperations protectedOperations,
            ITextBufferFactoryService textBufferFactoryService,
            ITextEditorFactoryService textEditorFactoryService,
            ITextDocumentFactoryService textDocumentFactoryService,
            IEditorOperationsFactoryService editorOperationsFactoryService,
            IContentTypeRegistryService contentTypeRegistryService,
            IFileSystem fileSystem,
            IDirectoryUtil directoryUtil) :
            base(
                protectedOperations,
                textBufferFactoryService,
                textEditorFactoryService,
                textDocumentFactoryService,
                editorOperationsFactoryService)
        {
            _protectedOperations = protectedOperations;
            _contentTypeRegistryService = contentTypeRegistryService;
            _fileSystem = fileSystem;
            _directoryUtil = directoryUtil;
        }

        public override void VimCreated(IVim vim)
        {
            _vim = vim;
            _vim.VimData.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public override void CloseAllOtherTabs(ITextView textView)
        {
            throw new NotImplementedException();
        }

        public override void CloseAllOtherWindows(ITextView textView)
        {
            throw new NotImplementedException();
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
            if (!TryGetVimViewInfo(textView, out IVimViewInfo vimViewInfo))
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

            if (TryLoadPath(filePath, out IWpfTextView createdTextView))
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

        public override FSharpOption<ITextView> LoadFileIntoNewWindow(string filePath, FSharpOption<int> line, FSharpOption<int> column)
        {
            try
            {
                var textDocument = TextDocumentFactoryService.CreateAndLoadTextDocument(filePath, TextBufferFactoryService.TextContentType);
                var wpfTextView = MainWindow.CreateTextView(textDocument.TextBuffer);
                MainWindow.AddNewTab(System.IO.Path.GetFileName(filePath), wpfTextView);

                if (line.IsSome())
                {
                    // Move the caret to its initial position.
                    if (column.IsSome())
                    {
                        wpfTextView.MoveCaretToLine(line.Value, column.Value);
                    }
                    else
                    {
                        // Default column implies moving to the first non-blank.
                        wpfTextView.MoveCaretToLine(line.Value);
                        var editorOperations = EditorOperationsFactoryService.GetEditorOperations(wpfTextView);
                        editorOperations.MoveToStartOfLineAfterWhiteSpace(false);
                    }
                }

                // Give the focus to the new buffer.
                var point = wpfTextView.Caret.Position.VirtualBufferPosition;
                NavigateTo(point);

                return FSharpOption.Create<ITextView>(wpfTextView);
            }
            catch (Exception ex)
            {
                _vim.ActiveStatusUtil.OnError(ex.Message);
                return FSharpOption<ITextView>.None;
            }
        }

        public override void Make(bool jumpToFirstError, string arguments)
        {
            _vim.ActiveStatusUtil.OnError(ErrorUnsupported);
        }

        public override void GoToWindow(ITextView textView, WindowKind windowKind, int count)
        {
            foreach (var vimWindow in _vimWindowManager.VimWindowList)
            {
                var list = vimWindow.VimViewInfoList;
                var i = 0;
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

                var target = -1;
                switch (windowKind)
                {
                    case WindowKind.Up:
                        target = i - 1;
                        break;
                    case WindowKind.Down:
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
            var textBuffer = point.Position.Snapshot.TextBuffer;
            foreach (var vimWindow in _vimWindowManager.VimWindowList)
            {
                foreach (var vimViewInfo in vimWindow.VimViewInfoList)
                {
                    if (vimViewInfo.TextView.TextBuffer == textBuffer)
                    {
                        Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() =>
                            {
                                // Select the tab.
                                vimWindow.TabItem.IsSelected = true;
                            }),
                            DispatcherPriority.ApplicationIdle);
                        Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() =>
                            {
                                // Move caret to point.
                                var textView = vimViewInfo.TextViewHost.TextView;
                                textView.Caret.MoveTo(point);

                                // Center the caret line in the window.
                                var caretLine = textView.GetCaretLine();
                                var span = caretLine.ExtentIncludingLineBreak;
                                var option = EnsureSpanVisibleOptions.AlwaysCenter;
                                textView.ViewScroller.EnsureSpanVisible(span, option);

                                // Focus the window.
                                Keyboard.Focus(textView.VisualElement);
                            }),
                            DispatcherPriority.ApplicationIdle);
                        return true;
                    }
                }
            }
            return false;
        }

        public override void RunCSharpScript(IVimBuffer vimBuffer, CallInfo callInfo, bool createEachTime)
        {
            throw new NotImplementedException();
        }

        public override void RunHostCommand(ITextView textView, string command, string argument)
        {
            var msg = $"Host Command Name='{command}' Argument='{argument}'";
            _vim.ActiveStatusUtil.OnStatus(msg);
        }

        public override bool Save(ITextBuffer textBuffer)
        {
            RaiseBeforeSave(textBuffer);
            return base.Save(textBuffer);
        }

        public override void SplitViewHorizontally(ITextView textView)
        {
            // First find the IVimViewInfo that contains this ITextView
            if (!TryGetVimViewInfo(textView, out IVimViewInfo vimViewInfo))
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

        public override void OpenListWindow(ListKind listKind)
        {
        }

        public override FSharpOption<ListItem> NavigateToListItem(ListKind listKind, NavigationKind navigationKind, FSharpOption<int> argument, bool hasBang)
        {
            return FSharpOption<ListItem>.None;
        }

#if VS_SPECIFIC_2015 || VS_SPECIFIC_2017

        // Visual Studio 2015 editor assemblies do not support the
        // multi-selection broker. Some versions of the Visual Studio 2017
        // editor assemblies do support them, but the current VsVim reference
        // assemblies do not.

#else

        public override IEnumerable<SelectedSpan> GetSelectedSpans(ITextView textView)
        {
            return GetSelectedSpansCommon(textView);
        }

        public override void SetSelectedSpans(ITextView textView, IEnumerable<SelectedSpan> selectedSpans)
        {
            SetSelectedSpansCommon(textView, selectedSpans.ToArray());
        }

        // TODO: duplicated code start
        private IEnumerable<SelectedSpan> GetSelectedSpansCommon(ITextView textView)
        {
            var broker = textView.GetMultiSelectionBroker();
            var primarySelection = broker.PrimarySelection;
            var secondarySelections = broker.AllSelections
                .Where(span => span != primarySelection)
                .Select(selection => GetSelectedSpan(selection));
            return new[] { GetSelectedSpan(primarySelection) }.Concat(secondarySelections);
        }

        private void SetSelectedSpansCommon(ITextView textView, SelectedSpan[] selectedSpans)
        {
            if (selectedSpans.Length == 1)
            {
                var selectedSpan = selectedSpans[0];
                textView.Caret.MoveTo(selectedSpan.CaretPoint);
                if (selectedSpan.Length == 0)
                {
                    textView.Selection.Clear();
                }
                else
                {
                    textView.Selection.Select(selectedSpan.StartPoint, selectedSpan.EndPoint);
                }
                return;
            }

            var selections = new Microsoft.VisualStudio.Text.Selection[selectedSpans.Length];
            for (var caretIndex = 0; caretIndex < selectedSpans.Length; caretIndex++)
            {
                selections[caretIndex] = GetSelection(selectedSpans[caretIndex]);
            }
            var broker = textView.GetMultiSelectionBroker();
            broker.SetSelectionRange(selections, selections[0]);
        }

        private static SelectedSpan GetSelectedSpan(Microsoft.VisualStudio.Text.Selection selection)
        {
            return new SelectedSpan(selection.InsertionPoint, selection.Start, selection.End);
        }

        private static Microsoft.VisualStudio.Text.Selection GetSelection(SelectedSpan span)
        {
            return new Microsoft.VisualStudio.Text.Selection(span.CaretPoint, span.StartPoint, span.EndPoint);
        }
        // TODO: duplicated code end

#endif

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
            if (!_directoryUtil.TryCreateDirectoryTextBuffer(filePath, out ITextBuffer textBuffer))
            {
                textView = null;
                return false;
            }

            textView = MainWindow.CreateTextView(textBuffer, PredefinedTextViewRoles.Interactive);
            return true;
        }
    }
}
