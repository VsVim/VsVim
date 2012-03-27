using NUnit.Framework;

namespace VsVim.UnitTest
{
    [TestFixture]
    public sealed class ResultTest
    {
        [Test]
        public void IsSuccess_Success()
        {
            Assert.IsTrue(Result.Success.IsSuccess);
            Assert.IsFalse(Result.Success.IsError);
        }
    }
}
