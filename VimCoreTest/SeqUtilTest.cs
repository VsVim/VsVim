using System.Linq;
using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class SeqUtilTest
    {
        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Test]
        public void SkipMax_Count()
        {
            var res = SeqUtil.skipMax(1, "foo");
            Assert.AreEqual(2, res.Count());
            Assert.AreEqual(2, res.Count());
            Assert.AreEqual(2, res.Count());
        }

        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Test]
        public void SkipMax_Count2()
        {
            var res = SeqUtil.skipMax(100, "foo");
            Assert.AreEqual(0, res.Count());
            Assert.AreEqual(0, res.Count());
            Assert.AreEqual(0, res.Count());
        }

        /// <summary>
        /// Ensure the count is not incorrectly cached leaving us with a changing IEnumerable
        /// </summary>
        [Test]
        public void SkipMax_Count3()
        {
            var res = SeqUtil.skipMax(0, "foo");
            Assert.AreEqual(3, res.Count());
            Assert.AreEqual(3, res.Count());
            Assert.AreEqual(3, res.Count());
        }
    }
}
