using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Xunit;
using Vim.VisualStudio.Implementation.Misc;
using Vim.UnitTest;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class CSharpAdapterTest : VimTestBase
    {
        private readonly CSharpAdapter _adapter;

        public CSharpAdapterTest()
        {
            _adapter = new CSharpAdapter();
        }

        private ITextView CreateCSharpTextView(params string[] lines)
        {
            var contentType = GetOrCreateContentType(Constants.CSharpContentType, "code");
            var textBuffer = CreateTextBuffer(contentType, lines);
            return TextEditorFactoryService.CreateTextView(textBuffer);
        }

        public sealed class IsInsertModePreferredTest : CSharpAdapterTest
        {
            /// <summary>
            /// If there is no selection then nothing to override
            /// </summary>
            [Fact]
            public void NoSelection()
            {
                var textView = CreateCSharpTextView();
                Assert.False(_adapter.IsInsertModePreferred(textView));
            }

            /// <summary>
            /// Set of patterns which should match
            /// </summary>
            [Fact]
            public void EventPattern_Is()
            {
                var all = new[] 
                {
                    new { Text = "foo += new EventHandler(bar)", NameLength = 3 },
                    new { Text = "foo += new EventHandler(bar_foo)", NameLength = 7 },
                    new { Text = "foo += new Event.Handler(bar)", NameLength = 3 },
                    new { Text = "foo += new Event.Handler(bar_foo)", NameLength = 7 },
                    new { Text = "foo+=new Event.Handler(bar_foo)", NameLength = 7 },
                    new { Text = "+= new EventHandler(bar)", NameLength = 3 },
                    new { Text = "+= Form1_Click;", NameLength = 11 },
                };

                var textView = CreateCSharpTextView();
                foreach (var item in all)
                {
                    var text = item.Text;
                    textView.SetText(text);
                    var span = new SnapshotSpan(textView.TextSnapshot, text.Length - item.NameLength - 1, item.NameLength);
                    textView.Selection.Select(span, isReversed: false);
                    Assert.True(_adapter.IsInsertModePreferred(textView));
                }
            }

            /// <summary>
            /// Set of patterns which shouldn't match
            /// </summary>
            [Fact]
            public void EventPattern_Not()
            {
                var all = new[] 
                {
                    new { Text = "new EventHandler(foo", NameLength = 3 },
                    new { Text = "+= EventHandler(foo", NameLength = 3 },
                    new { Text = "+= someExpr", NameLength = 8 }
                };

                var textView = CreateCSharpTextView();
                foreach (var item in all)
                {
                    var text = item.Text;
                    textView.SetText(text);
                    var span = new SnapshotSpan(textView.TextSnapshot, text.Length - item.NameLength, item.NameLength);
                    textView.Selection.Select(span, isReversed: false);
                    Assert.False(_adapter.IsInsertModePreferred(textView));
                }
            }

            /// <summary>
            /// In Visual Studio 2012 the event add pattern has a different default that we need to 
            /// recognize 
            /// </summary>
            [Fact]
            public void VisualStudio2012EventPattern()
            {
                var textView = CreateCSharpTextView();
                textView.SetText("x += Form1_Click;");
                var span = textView.TextBuffer.GetSpan(5, 11);
                textView.Selection.Select(span, isReversed: false);
                Assert.True(_adapter.IsInsertModePreferred(textView));
            }
        }
    }
}
