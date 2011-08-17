using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class CharUtilTest
    {
        [Test]
        public void Rot13_AllLettersMapBackLower()
        {
            foreach (var cur in KeyInputUtilTest.CharsLettersLower)
            {
                var rot = CharUtil.ChangeRot13(cur);
                var end = CharUtil.ChangeRot13(rot);
                Assert.AreEqual(cur, end);
            }
        }

        [Test]
        public void Rot13_AllLettersMapBackUpper()
        {
            foreach (var cur in KeyInputUtilTest.CharsLettersUpper)
            {
                var rot = CharUtil.ChangeRot13(cur);
                var end = CharUtil.ChangeRot13(rot);
                Assert.AreEqual(cur, end);
            }
        }

        /// <summary>
        /// Make sure that we can handle simple add operations with alpha characters
        /// </summary>
        [Test]
        public void AddAplha_Simple()
        {
            Assert.AreEqual('b', CharUtil.AlphaAdd(1, 'a'));
            Assert.AreEqual('c', CharUtil.AlphaAdd(2, 'a'));
            Assert.AreEqual('B', CharUtil.AlphaAdd(1, 'A'));
            Assert.AreEqual('C', CharUtil.AlphaAdd(2, 'A'));
        }

        /// <summary>
        /// Going past 'Z' should return simply 'Z'
        /// </summary>
        [Test]
        public void AddAlpha_PastUpperBound()
        {
            Assert.AreEqual('z', CharUtil.AlphaAdd(1, 'z'));
            Assert.AreEqual('Z', CharUtil.AlphaAdd(1, 'Z'));
        }

        /// <summary>
        /// Going past 'A' should return simply 'A'
        /// </summary>
        [Test]
        public void AddAlpha_PastLowerBound()
        {
            Assert.AreEqual('a', CharUtil.AlphaAdd(-1, 'a'));
            Assert.AreEqual('A', CharUtil.AlphaAdd(-1, 'A'));
        }
    }
}
