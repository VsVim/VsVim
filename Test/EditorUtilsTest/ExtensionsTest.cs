using System.Linq;
using Microsoft.VisualStudio.Utilities;
using Xunit;

namespace EditorUtils.UnitTest
{
    public sealed class ExtensionsTest : EditorHostTest
    {
        [Fact]
        public void GetSourceBuffersRecursive_Simple()
        {
            var textBuffer1 = CreateTextBuffer("hello");
            var textBuffer2 = CreateTextBuffer(" world");
            var projectionBuffer = CreateProjectionBuffer(
                textBuffer1.GetExtent(),
                textBuffer2.GetExtent());

            Assert.Equal("hello world", projectionBuffer.GetLine(0).GetText());
            var all = projectionBuffer.GetSourceBuffersRecursive().ToList();
            Assert.Equal(2, all.Count);
            Assert.True(all.Contains(textBuffer1));
            Assert.True(all.Contains(textBuffer2));
        }

        [Fact]
        public void GetSourceBuffersRecursive_Nested()
        {
            var textBuffer1 = CreateTextBuffer("hello");
            var textBuffer2 = CreateTextBuffer(" ");
            var textBuffer3 = CreateTextBuffer("world");
            var projectionBuffer1 = CreateProjectionBuffer(
                textBuffer1.GetExtent(),
                textBuffer2.GetExtent());
            var projectionBuffer2 = CreateProjectionBuffer(
                projectionBuffer1.GetExtent(),
                textBuffer3.GetExtent());

            Assert.Equal("hello world", projectionBuffer2.GetLine(0).GetText());
            var all = projectionBuffer2.GetSourceBuffersRecursive().ToList();
            Assert.Equal(3, all.Count);
            Assert.True(all.Contains(textBuffer1));
            Assert.True(all.Contains(textBuffer2));
            Assert.True(all.Contains(textBuffer3));
        }

        [Fact]
        public void TryGetPropertySafe_Found()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, "target");

            string value;
            Assert.True(col.TryGetPropertySafe(key, out value));
            Assert.Equal("target", value);
        }

        [Fact]
        public void TryGetPropertySafe_NotFound()
        {
            var col = new PropertyCollection();
            var key = new object();

            string value;
            Assert.False(col.TryGetPropertySafe(key, out value));
        }

        /// <summary>
        /// Make sure it doesn't throw if the value is the wrong type
        /// </summary>
        [Fact]
        public void TryGetPropertySafe_WrongType()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, this);

            string value;
            Assert.False(col.TryGetPropertySafe(key, out value));
        }

        [Fact]
        public void GetLastLine_WithNonEmptyLastLine_ReturnsCorrectLastLine()
        {
            var textBuffer = CreateTextBuffer("hello","World", "Foo");
            var lastLine = textBuffer.GetSpan(0, textBuffer.CurrentSnapshot.Length).GetLastLine();

            Assert.True(lastLine.LineNumber == 2);
        }

        [Fact]
        public void GetLastLine_WithEmptyLastLine_ReturnsCorrectLastLine()
        {
            var textBuffer = CreateTextBuffer("hello","World", "");
            var lastLine = textBuffer.GetSpan(0, textBuffer.CurrentSnapshot.Length).GetLastLine();

            Assert.True(lastLine.LineNumber == 2);
        }



    }
}
