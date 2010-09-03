using System.Linq;
using NUnit.Framework;
using Vim;

namespace VimCore.Test
{
    [TestFixture]
    public class SeqUtilTest
    {
        [Test]
        [Description("Make sure we don't wrongly cache the count and have a changing IEnumerable")]
        public void SkipMax1()
        {
            var res = SeqUtil.skipMax(1, "foo");
            Assert.AreEqual(2, res.Count());
            Assert.AreEqual(2, res.Count());
            Assert.AreEqual(2, res.Count());
        }

        [Test]
        [Description("Make sure we don't wrongly cache the count and have a changing IEnumerable")]
        public void SkipMax2()
        {
            var res = SeqUtil.skipMax(100, "foo");
            Assert.AreEqual(0, res.Count());
            Assert.AreEqual(0, res.Count());
            Assert.AreEqual(0, res.Count());
        }

        [Test]
        [Description("Make sure we don't wrongly cache the count and have a changing IEnumerable")]
        public void SkipMax3()
        {
            var res = SeqUtil.skipMax(0, "foo");
            Assert.AreEqual(3, res.Count());
            Assert.AreEqual(3, res.Count());
            Assert.AreEqual(3, res.Count());
        }
    }
}
