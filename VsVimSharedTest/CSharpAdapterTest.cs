using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using VsVim.Implementation;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class CSharpAdapterTest : EditorUtils.EditorHost
    {
        private CSharpAdapter _adapter;

        [SetUp]
        public void SetUp()
        {
            _adapter = new CSharpAdapter();
        }

        private ITextView CreateCSharpTextView(params string[] lines)
        {
            var contentType = GetOrCreateContentType(Constants.CSharpContentType, "code");
            var textBuffer = CreateTextBuffer(contentType, lines);
            return TextEditorFactoryService.CreateTextView(textBuffer);
        }

        /// <summary>
        /// If there is no selection then nothing to override
        /// </summary>
        [Test]
        public void IsInsertModePreferred_NoSelection()
        {
            var textView = CreateCSharpTextView();
            Assert.IsFalse(_adapter.IsInsertModePreferred(textView));
        }

        /// <summary>
        /// Set of patterns which should match
        /// </summary>
        [Test]
        public void IsInsertModePreferred_EventPattern_Is()
        {
            var all = new[] 
                {
                    new { Text = "foo += new EventHandler(bar)", NameLength = 3 },
                    new { Text = "foo += new EventHandler(bar_foo)", NameLength = 7 },
                    new { Text = "foo += new Event.Handler(bar)", NameLength = 3 },
                    new { Text = "foo += new Event.Handler(bar_foo)", NameLength = 7 },
                    new { Text = "foo+=new Event.Handler(bar_foo)", NameLength = 7 },
                    new { Text = "+= new EventHandler(bar)", NameLength = 3 },
                };

            var textView = CreateCSharpTextView();
            foreach (var item in all)
            {
                var text = item.Text;
                textView.SetText(text);
                var span = new SnapshotSpan(textView.TextSnapshot, text.Length - item.NameLength - 1, item.NameLength);
                textView.Selection.Select(span, isReversed: false);
                Assert.IsTrue(_adapter.IsInsertModePreferred(textView));
            }
        }

        /// <summary>
        /// Set of patterns which shouldn't match
        /// </summary>
        [Test]
        public void IsInsertModePreferred_EventPattern_Not()
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
                Assert.IsFalse(_adapter.IsInsertModePreferred(textView));
            }
        }
    }
}
