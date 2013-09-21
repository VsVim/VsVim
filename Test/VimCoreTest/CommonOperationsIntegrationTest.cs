using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class CommonOperationsIntegrationTest : VimTestBase
    {
        private IVimBuffer _vimBuffer;
        private IWpfTextView _textView;
        private ITextBuffer _textBuffer;
        private IVimGlobalSettings _globalSettings;
        private CommonOperations _commonOperationsRaw;
        private ICommonOperations _commonOperations;

        private void Create(params string[] lines)
        {
            _globalSettings = Vim.GlobalSettings;
            _vimBuffer = CreateVimBuffer(lines);
            _textView = (IWpfTextView)_vimBuffer.TextView;
            _textBuffer = _vimBuffer.TextBuffer;
            _commonOperationsRaw = (CommonOperations)CommonOperationsFactory.GetCommonOperations(_vimBuffer.VimBufferData);
            _commonOperations = _commonOperationsRaw;
        }

        public abstract class ScrollOffsetTest : CommonOperationsIntegrationTest
        {
            private int _lastLineNumber = 0;

            protected ScrollOffsetTest()
            {
                Create(KeyInputUtilTest.CharLettersLower.Select(x => x.ToString()).ToArray());
                _lastLineNumber = _textBuffer.CurrentSnapshot.LineCount - 1;
                _textView.SetVisibleLineCount(5);
            }

            private void AssertFirstLine(int lineNumber)
            {
                var line = _textView.TextViewLines.FirstVisibleLine;
                var snapshotLine = line.Start.GetContainingLine();
                Assert.Equal(lineNumber, snapshotLine.LineNumber);
            }

            private void AssertLastLine(int lineNumber)
            {
                var line = _textView.TextViewLines.LastVisibleLine;
                var snapshotLine = line.End.GetContainingLine();
                Assert.Equal(lineNumber, snapshotLine.LineNumber);
            }

            public sealed class TopTest : ScrollOffsetTest
            {
                [Fact]
                public void Disabled()
                {
                    _globalSettings.ScrollOffset = 0;
                    _textView.MoveCaretToLine(1);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertFirstLine(0);
                }

                [Fact]
                public void Simple()
                {
                    _globalSettings.ScrollOffset = 1;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToBottom();
                    _textView.MoveCaretToLine(2);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertFirstLine(1);
                }

                /// <summary>
                /// Handle the case where the scroll would be to the top of the screen
                /// </summary>
                [Fact]
                public void ScrollToTop()
                {
                    _globalSettings.ScrollOffset = 2;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToBottom();
                    _textView.MoveCaretToLine(2);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertFirstLine(0);
                }

                /// <summary>
                /// Handle the case where the correct scroll offset would be above the first line
                /// in the view.  Should just stop at the first line 
                /// </summary>
                [Fact]
                public void ScrollAboveTop()
                {
                    _globalSettings.ScrollOffset = 3;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToBottom();
                    _textView.MoveCaretToLine(1);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertFirstLine(0);
                }

                /// <summary>
                /// Handle the case where the 'scrolloff' value is larger than half the lines in the 
                /// visible screen.  At that point the scroll should just center the caret 
                /// </summary>
                [Fact]
                public void OffsetTooBig()
                {
                    _globalSettings.ScrollOffset = 100;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToBottom();
                    _textView.MoveCaretToLine(7);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertFirstLine(5);
                }
            }

            public sealed class BottomTest : ScrollOffsetTest
            {
                [Fact]
                public void Disabled()
                {
                    _globalSettings.ScrollOffset = 0;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToTop();
                    _textView.MoveCaretToLine(_lastLineNumber);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertLastLine(4);
                }

                [Fact]
                public void Simple()
                {
                    _globalSettings.ScrollOffset = 1;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToTop();
                    _textView.MoveCaretToLine(4);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertLastLine(5);
                }

                [Fact]
                public void ScrollToBottom()
                {
                    _globalSettings.ScrollOffset = 1;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToTop();
                    _textView.MoveCaretToLine(_lastLineNumber - 1);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertLastLine(_lastLineNumber);
                }

                [Fact]
                public void ScrollBelowBottom()
                {
                    _globalSettings.ScrollOffset = 2;
                    _textView.SetVisibleLineCount(5);
                    _textView.ScrollToTop();
                    _textView.MoveCaretToLine(_lastLineNumber - 1);
                    _commonOperationsRaw.AdjustTextViewForScrollOffset();
                    AssertLastLine(_lastLineNumber);
                }
            }
        }
    }
}
