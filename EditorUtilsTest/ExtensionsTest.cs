using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using NUnit.Framework;

namespace EditorUtils.UnitTest
{
    [TestFixture]
    public sealed class ExtensionsTest : EditorTestBase
    {
        [Test]
        public void GetSourceBuffersRecursive_Simple()
        {
            var textBuffer1 = CreateTextBuffer("hello");
            var textBuffer2 = CreateTextBuffer(" world");
            var projectionBuffer = CreateProjectionBuffer(
                textBuffer1.GetExtent(),
                textBuffer2.GetExtent());

            Assert.AreEqual("hello world", projectionBuffer.GetLine(0).GetText());
            var all = projectionBuffer.GetSourceBuffersRecursive().ToList();
            Assert.AreEqual(2, all.Count);
            Assert.IsTrue(all.Contains(textBuffer1));
            Assert.IsTrue(all.Contains(textBuffer2));
        }

        [Test]
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

            Assert.AreEqual("hello world", projectionBuffer2.GetLine(0).GetText());
            var all = projectionBuffer2.GetSourceBuffersRecursive().ToList();
            Assert.AreEqual(3, all.Count);
            Assert.IsTrue(all.Contains(textBuffer1));
            Assert.IsTrue(all.Contains(textBuffer2));
            Assert.IsTrue(all.Contains(textBuffer3));
        }

        [Test]
        public void TryGetPropertySafe_Found()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, "target");

            string value;
            Assert.IsTrue(col.TryGetPropertySafe(key, out value));
            Assert.AreEqual("target", value);
        }

        [Test]
        public void TryGetPropertySafe_NotFound()
        {
            var col = new PropertyCollection();
            var key = new object();

            string value;
            Assert.IsFalse(col.TryGetPropertySafe(key, out value));
        }

        /// <summary>
        /// Make sure it doesn't throw if the value is the wrong type
        /// </summary>
        [Test]
        public void TryGetPropertySafe_WrongType()
        {
            var col = new PropertyCollection();
            var key = new object();
            col.AddProperty(key, this);

            string value;
            Assert.IsFalse(col.TryGetPropertySafe(key, out value));
        }
    }
}
