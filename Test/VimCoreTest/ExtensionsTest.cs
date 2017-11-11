using System.Linq;
using Microsoft.VisualStudio.Utilities;
using Xunit;
using Vim.Extensions;
using Vim.EditorHost;

namespace Vim.UnitTest
{
    public sealed class ExtensionsTest : VimTestBase
    {
        [WpfFact]
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
            Assert.Contains(textBuffer1, all);
            Assert.Contains(textBuffer2, all);
        }

        [WpfFact]
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
            Assert.Contains(textBuffer1, all);
            Assert.Contains(textBuffer2, all);
            Assert.Contains(textBuffer3, all);
        }

        [WpfFact]
        public void TryGetPropertySafe_Found()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, "target");

            string value;
            Assert.True(col.TryGetPropertySafe(key, out value));
            Assert.Equal("target", value);
        }

        [WpfFact]
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
        [WpfFact]
        public void TryGetPropertySafe_WrongType()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, this);

            string value;
            Assert.False(col.TryGetPropertySafe(key, out value));
        }
    }
}
