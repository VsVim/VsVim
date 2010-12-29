using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;
using Vim.UI.Wpf;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    internal sealed class VsVimHost : VimHost
    {
        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly ITextManager _textManager;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly _DTE _dte;

        internal _DTE DTE
        {
            get { return _dte; }
        }

        [ImportingConstructor]
        internal VsVimHost(
            ITextBufferUndoManagerProvider undoManagerProvider,
            IVsEditorAdaptersFactoryService editorAdaptersFactoryService,
            ITextManager textManager,
            SVsServiceProvider serviceProvider)
        {
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _textManager = textManager;
        }

        private bool SafeExecuteCommand(string command, string args = "")
        {
            try
            {
                _dte.ExecuteCommand(command, args);
                return true;
            }
            catch (Exception)
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
                var span = TssUtil.FindCurrentFullWordSpan(caretPoint, WordKind.NormalWord);
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

        private bool OpenFileCore(string fileName)
        {
            if (SafeExecuteCommand("File.OpenFile", fileName))
            {
                return true;
            }

            var list = _dte.GetProjectItems(fileName);
            if (list.Any())
            {
                var item = list.First();
                var result = item.Open(EnvDTE.Constants.vsViewKindPrimary);
                result.Activate();
                return true;
            }

            return false;
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
            return GoToDefinitionCore(_textManager.ActiveTextView, null);
        }

        public override bool GoToMatch()
        {
            return SafeExecuteCommand("Edit.GoToBrace");
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

        public override bool Save(ITextView textView)
        {
            return _textManager.Save(textView).IsSuccess;
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

        public override bool SaveAllFiles()
        {
            var anyFailed = false;
            var all = _textManager.TextViews;
            foreach (var textView in all)
            {
                if (_textManager.Save(textView).IsError)
                {
                    anyFailed = true;
                }
            }

            return anyFailed;
        }

        public override void CloseAllFiles(bool checkDirty)
        {
            var all = _textManager.TextViews.ToList();
            foreach (var textView in all)
            {
                _textManager.CloseBuffer(textView, checkDirty);
            }
        }

        public override void Close(ITextView textView, bool checkDirty)
        {
            _textManager.CloseView(textView, checkDirty);
        }

        public override void GoToNextTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.NextDocumentWindow");
                count--;
            }
        }

        public override void GoToPreviousTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.PreviousDocumentWindow");
                count--;
            }
        }

        public override void BuildSolution()
        {
            SafeExecuteCommand("Build.BuildSolution");
        }

        public override void SplitView(ITextView textView)
        {
            _textManager.SplitView(textView);
        }

        public override void MoveViewDown(ITextView textView)
        {
            _textManager.MoveViewDown(textView);
        }

        public override void MoveViewUp(ITextView textView)
        {
            _textManager.MoveViewUp(textView);
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

        public override bool GoToFile(string fileName)
        {
            return OpenFileCore(fileName);
        }
    }
}
