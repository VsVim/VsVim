using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class TextChangeTest
    {
        /// <summary>
        /// The InsertText for a simple insert is just the text
        /// </summary>
        [Fact]
        public void InsertText_Simple()
        {
            var textChange = TextChange.NewInsert("dog");
            Assert.Equal("dog", textChange.InsertText.Value);
        }

        /// <summary>
        /// Combined inserts should just combine the values
        /// </summary>
        [Fact]
        public void InsertText_CombinedInsert()
        {
            var textChange = TextChange.NewCombination(
                TextChange.NewInsert("hello "),
                TextChange.NewInsert("world"));
            Assert.Equal("hello world", textChange.InsertText.Value);
        }

        /// <summary>
        /// A naked delete should not have an InsertText
        /// </summary>
        [Fact]
        public void InsertText_DeleteLeft()
        {
            var textChange = TextChange.NewDeleteLeft(1);
            Assert.True(textChange.InsertText.IsNone());
        }

        /// <summary>
        /// If the delete doesn't remove all of the text then there is still an insert
        /// </summary>
        [Fact]
        public void InsertText_SmallDeleteLeft()
        {
            var textChange = TextChange.NewCombination(
                TextChange.NewInsert("dogs"),
                TextChange.NewDeleteLeft(1));
            Assert.Equal("dog", textChange.InsertText.Value);
        }

        /// <summary>
        /// If the delete removes all of the text then there is no insert
        /// </summary>
        [Fact]
        public void InsertText_BigDeleteLeft()
        {
            var textChange = TextChange.NewCombination(
                TextChange.NewInsert("dogs"),
                TextChange.NewDeleteLeft(10));
            Assert.True(textChange.InsertText.IsNone());
        }
    }
}
