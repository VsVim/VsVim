using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Media;
using EnvDTE;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using Vim;
using Vim.Extensions;

namespace VsVim
{
    /// <summary>
    /// Implement the IVimHost interface for Visual Studio functionality.  It's not responsible for 
    /// the relationship with the IVimBuffer, merely implementing the host functionality
    /// </summary>
    [Export(typeof(IVimHost))]
    internal sealed class VsVimHost : IVimHost
    {
        internal const string CommandNameGoToDefinition = "Edit.GoToDefinition";

        private readonly ITextManager _textManager;
        private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
        private readonly ITextBufferUndoManagerProvider _undoManagerProvider;
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
            _undoManagerProvider = undoManagerProvider;
            _editorAdaptersFactoryService = editorAdaptersFactoryService;
            _dte = (_DTE)serviceProvider.GetService(typeof(_DTE));
            _textManager = textManager;
        }

        private void UpdateStatus(string text)
        {
            _dte.StatusBar.Text = text;
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
            else
            {
                return SafeExecuteCommand(CommandNameGoToDefinition);
            }
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

            var names = _dte.GetProjects().SelectMany(x => x.GetProjecItems()).Select(x => x.Name).ToList();
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
        private void FormatLines(ITextView textView, SnapshotLineSpan range)
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

        #region IVimHost

        void IVimHost.Beep()
        {
            SystemSounds.Beep.Play();
        }

        bool IVimHost.GoToDefinition()
        {
            return GoToDefinitionCore(_textManager.ActiveTextView, null);
        }

        bool IVimHost.GoToMatch()
        {
            return SafeExecuteCommand("Edit.GoToBrace");
        }

        void IVimHost.ShowOpenFileDialog()
        {
            SafeExecuteCommand("Edit.OpenFile");
        }

        bool IVimHost.NavigateTo(VirtualSnapshotPoint point)
        {
            return _textManager.NavigateTo(point);
        }

        string IVimHost.GetName(ITextBuffer buffer)
        {
            var vsTextLines = _editorAdaptersFactoryService.GetBufferAdapter(buffer) as IVsTextLines;
            if (vsTextLines == null)
            {
                return String.Empty;
            }
            return vsTextLines.GetFileName();
        }

        void IVimHost.Save(ITextView textView)
        {
            _textManager.Save(textView);
        }

        void IVimHost.SaveCurrentFileAs(string fileName)
        {
            SafeExecuteCommand("File.SaveSelectedItemsAs " + fileName);
        }

        void IVimHost.SaveAllFiles()
        {
            var all = _textManager.TextViews;
            foreach (var textView in all)
            {
                _textManager.Save(textView);
            }
        }

        void IVimHost.Close(ITextView textView, bool checkDirty)
        {
            _textManager.CloseBuffer(textView, checkDirty);
        }

        void IVimHost.CloseAllFiles(bool checkDirty)
        {
            var all = _textManager.TextViews.ToList();
            foreach (var textView in all)
            {
                _textManager.CloseBuffer(textView, checkDirty);
            }
        }

        void IVimHost.CloseView(ITextView textView, bool checkDirty)
        {
            _textManager.CloseView(textView, checkDirty);
        }

        void IVimHost.GoToNextTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.NextDocumentWindow");
                count--;
            }
        }

        void IVimHost.GoToPreviousTab(int count)
        {
            while (count > 0)
            {
                SafeExecuteCommand("Window.PreviousDocumentWindow");
                count--;
            }
        }

        void IVimHost.BuildSolution()
        {
            SafeExecuteCommand("Build.BuildSolution");
        }

        void IVimHost.SplitView(ITextView textView)
        {
            _textManager.SplitView(textView);
        }

        void IVimHost.MoveViewDown(ITextView textView)
        {
            _textManager.MoveViewDown(textView);
        }

        void IVimHost.MoveViewUp(ITextView textView)
        {
            _textManager.MoveViewUp(textView);
        }

        bool IVimHost.GoToGlobalDeclaration(ITextView textView, string target)
        {
            return GoToDefinitionCore(textView, target);
        }

        bool IVimHost.GoToLocalDeclaration(ITextView textView, string target)
        {
            // This is technically incorrect as it should prefer local declarations. However 
            // there is currently no better way in Visual Studio.  Added this method though
            // so it's easier to plug in later should such an API become available
            return GoToDefinitionCore(textView, target);
        }

        bool IVimHost.GoToFile(string fileName)
        {
            return OpenFileCore(fileName);
        }

        void IVimHost.FormatLines(ITextView textView, SnapshotLineSpan lineSpan)
        {
            FormatLines(textView, lineSpan);
        }

        #endregion
    }
}
