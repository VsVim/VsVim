using System;
using System.Linq;
using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class TextLineTest
    {
        /// <summary>
        /// The GetTextLines and CreateString method should have completely fidelity with 
        /// each other
        /// </summary>
        [Test]
        public void BothWays()
        {
            Action<string> action =
                text =>
                {
                    var lines = TextLine.GetTextLines(text);
                    var otherText = TextLine.CreateString(lines);
                    Assert.AreEqual(text, otherText);
                };

            action("");
            action(Environment.NewLine);
            action("dog" + Environment.NewLine);
            action("dog" + Environment.NewLine + Environment.NewLine);
            action("dog" + Environment.NewLine + "cat" + Environment.NewLine);
        }

        /// <summary>
        /// Ensure that this works with the empty string case.  This is a very common case w
        /// which is subtly easy to get wrong
        /// </summary>
        [Test]
        public void GetTextLines_EmptyString()
        {
            var lines = TextLine.GetTextLines("");
            Assert.AreEqual(1, lines.Count);
            var textLine = lines.Single();
            Assert.AreEqual(String.Empty, textLine.Text);
            Assert.AreEqual(String.Empty, textLine.NewLine);
        }

        /// <summary>
        /// Test a fairly standard case of two lines of text where only one have new line
        /// </summary>
        [Test]
        public void GetTextLines_TwoLines()
        {
            var lines = TextLine.GetTextLines("dog" + Environment.NewLine + "cat").ToList();
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("dog", lines[0].Text);
            Assert.AreEqual(Environment.NewLine, lines[0].NewLine);
            Assert.AreEqual("cat", lines[1].Text);
            Assert.AreEqual(String.Empty, lines[1].NewLine);
        }

        /// <summary>
        /// Test an irregular line ending case
        /// </summary>
        [Test]
        public void GetTextLines_IrregularEnding()
        {
            var lines = TextLine.GetTextLines("dog\rcat").ToList();
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("dog", lines[0].Text);
            Assert.AreEqual("\r", lines[0].NewLine);
            Assert.AreEqual("cat", lines[1].Text);
            Assert.AreEqual(String.Empty, lines[1].NewLine);
        }
    }
}
