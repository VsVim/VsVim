using Xunit;

namespace Vim.UnitTest
{
    public sealed class CharUtilTest
    {
        [Fact]
        public void Rot13_AllLettersMapBackLower()
        {
            foreach (var cur in KeyInputUtilTest.CharLettersLower)
            {
                var rot = CharUtil.ChangeRot13(cur);
                var end = CharUtil.ChangeRot13(rot);
                Assert.Equal(cur, end);
            }
        }

        [Fact]
        public void Rot13_AllLettersMapBackUpper()
        {
            foreach (var cur in KeyInputUtilTest.CharLettersUpper)
            {
                var rot = CharUtil.ChangeRot13(cur);
                var end = CharUtil.ChangeRot13(rot);
                Assert.Equal(cur, end);
            }
        }

        /// <summary>
        /// Make sure that we can handle simple add operations with alpha characters
        /// </summary>
        [Fact]
        public void AddAplha_Simple()
        {
            Assert.Equal('b', CharUtil.AlphaAdd(1, 'a'));
            Assert.Equal('c', CharUtil.AlphaAdd(2, 'a'));
            Assert.Equal('B', CharUtil.AlphaAdd(1, 'A'));
            Assert.Equal('C', CharUtil.AlphaAdd(2, 'A'));
        }

        /// <summary>
        /// Going past 'Z' should return simply 'Z'
        /// </summary>
        [Fact]
        public void AddAlpha_PastUpperBound()
        {
            Assert.Equal('z', CharUtil.AlphaAdd(1, 'z'));
            Assert.Equal('Z', CharUtil.AlphaAdd(1, 'Z'));
        }

        /// <summary>
        /// Going past 'A' should return simply 'A'
        /// </summary>
        [Fact]
        public void AddAlpha_PastLowerBound()
        {
            Assert.Equal('a', CharUtil.AlphaAdd(-1, 'a'));
            Assert.Equal('A', CharUtil.AlphaAdd(-1, 'A'));
        }
    }
}
