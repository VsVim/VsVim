using System;
using System.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Vim.UI.Wpf
{
    public abstract class VimHost : IVimHost
    {
        private readonly ITextDocumentFactoryService _textDocumentFactoryService;

        protected VimHost(ITextDocumentFactoryService textDocumentFactoryService)
        {
            _textDocumentFactoryService = textDocumentFactoryService;
        }

        public virtual void Beep()
        {
            SystemSounds.Beep.Play();
        }

        public abstract void BuildSolution();

        public virtual void Close(ITextView textView, bool checkDirty)
        {
            textView.Close();
        }

        public abstract void CloseAllFiles(bool checkDirty);

        public void EnsureVisible(ITextView textView, SnapshotPoint point)
        {
            const double roundOff = 0.01;
            var textViewLine = textView.GetTextViewLineContainingBufferPosition(point);

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

            // Now that the line is visible we need to potentially do some horizontal scrolling
            // take make sure that it's on screen
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

        public abstract void FormatLines(ITextView textView, SnapshotLineRange range);

        public abstract string GetName(ITextBuffer value);

        public abstract bool GoToDefinition();

        public abstract bool GoToGlobalDeclaration(ITextView textView, string name);

        public abstract bool GoToLocalDeclaration(ITextView textView, string name);

        public abstract void GoToNextTab(Direction direction, int count);

        public abstract void GoToTab(int index);

        public virtual bool IsDirty(ITextBuffer textbuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textbuffer, out document))
            {
                return false;
            }

            return document.IsDirty;
        }

        public abstract HostResult LoadFileIntoExistingWindow(string filePath, ITextBuffer textbuffer);

        public abstract HostResult LoadFileIntoNewWindow(string filePath);

        public abstract void MoveViewDown(ITextView value);

        public abstract void MoveViewUp(ITextView value);

        public abstract void MoveViewLeft(ITextView value);

        public abstract void MoveViewRight(ITextView value);

        public abstract bool NavigateTo(VirtualSnapshotPoint point);

        public virtual bool Reload(ITextBuffer textBuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document))
            {
                return false;
            }

            document.Reload(EditOptions.DefaultMinimalChange);
            return true;
        }

        public virtual bool Save(ITextBuffer textBuffer)
        {
            ITextDocument document;
            if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document))
            {
                return false;
            }

            document.Save();
            return true;
        }

        public abstract bool SaveAllFiles();

        public abstract bool SaveTextAs(string text, string filePath);

        public abstract void ShowOpenFileDialog();

        public abstract HostResult SplitViewHorizontally(ITextView value);

        public abstract HostResult SplitViewVertically(ITextView value);
    }
}
