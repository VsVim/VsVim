using System;
using EditorUtils.UnitTest;
using Microsoft.VisualStudio.Text;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class CharacterSpanTest : VimTestBase
    {
        private ITextBuffer _textBuffer;

        private void Create(params string[] lines)
        {
            _textBuffer = CreateTextBuffer(lines);
        }

        /// <summary>
        /// Verify End is correct for a single line
        /// </summary>
        [Test]
        public void End_SingleLine()
        {
            Create("cats", "dog");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 1, 2);
            Assert.AreEqual("at", characterSpan.Span.GetText());
        }

        /// <summary>
        /// Verify End is correct for multiple lines
        /// </summary>
        [Test]
        public void End_MultiLine()
        {
            Create("cats", "dogs");
            var characterSpan = new CharacterSpan(_textBuffer.GetPoint(1), 2, 2);
            Assert.AreEqual("ats" + Environment.NewLine + "do", characterSpan.Span.GetText());
        }

        /// <summary>
        /// Make sure operator equality functions as expected
        /// </summary>
        [Test]
        public void Equality_Operator()
        {
            Create("cat", "dog");
            EqualityUtil.RunAll(
                (left, right) => left == right,
                (left, right) => left != right,
                false,
                false,
                EqualityUnit.Create(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2))
                    .WithEqualValues(new CharacterSpan(_textBuffer.GetPoint(0), 1, 2))
                    .WithNotEqualValues(new CharacterSpan(_textBuffer.GetPoint(1), 1, 2)));
        }
    }
}
