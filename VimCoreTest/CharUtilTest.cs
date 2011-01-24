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
    }
}
