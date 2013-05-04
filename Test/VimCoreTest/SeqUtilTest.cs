using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class SeqUtilTest
    {
        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Fact]
        public void SkipMax_Count()
        {
            var res = SeqUtil.skipMax(1, "foo");
            Assert.Equal(2, res.Count());
            Assert.Equal(2, res.Count());
            Assert.Equal(2, res.Count());
        }

        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Fact]
        public void SkipMax_Count2()
        {
            var res = SeqUtil.skipMax(100, "foo");
            Assert.Equal(0, res.Count());
            Assert.Equal(0, res.Count());
            Assert.Equal(0, res.Count());
        }

        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Fact]
        public void SkipMax_Count3()
        {
            var res = SeqUtil.skipMax(0, "foo");
            Assert.Equal(3, res.Count());
            Assert.Equal(3, res.Count());
            Assert.Equal(3, res.Count());
        }
    }
}
