using System;
using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class TextLineTest
    {
        /// <summary>
        /// The GetTextLines and CreateString method should have completely fidelity with 
        /// each other
        /// </summary>
        [Fact]
        public void BothWays()
        {
            Action<string> action =
                text =>
                {
                    var lines = TextLine.GetTextLines(text);
                    var otherText = TextLine.CreateString(lines);
                    Assert.Equal(text, otherText);
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
        [Fact]
        public void GetTextLines_EmptyString()
        {
            var lines = TextLine.GetTextLines("");
            Assert.Equal(1, lines.Count);
            var textLine = lines.Single();
            Assert.Equal(String.Empty, textLine.Text);
            Assert.Equal(String.Empty, textLine.NewLine);
        }

        /// <summary>
        /// Test a fairly standard case of two lines of text where only one have new line
        /// </summary>
        [Fact]
        public void GetTextLines_TwoLines()
        {
            var lines = TextLine.GetTextLines("dog" + Environment.NewLine + "cat").ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal("dog", lines[0].Text);
            Assert.Equal(Environment.NewLine, lines[0].NewLine);
            Assert.Equal("cat", lines[1].Text);
            Assert.Equal(String.Empty, lines[1].NewLine);
        }

        /// <summary>
        /// Test an irregular line ending case
        /// </summary>
        [Fact]
        public void GetTextLines_IrregularEnding()
        {
            var lines = TextLine.GetTextLines("dog\rcat").ToList();
            Assert.Equal(2, lines.Count);
            Assert.Equal("dog", lines[0].Text);
            Assert.Equal("\r", lines[0].NewLine);
            Assert.Equal("cat", lines[1].Text);
            Assert.Equal(String.Empty, lines[1].NewLine);
        }
    }
}
