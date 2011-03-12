using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class RegisterTest
    {
        [Test]
        public void ValueBackingTest1()
        {
            var backing = new MockRegisterValueBacking();
            var reg = new Register(RegisterName.Unnamed, backing);
            reg.RegisterValue = RegisterValue.OfString("foo", OperationKind.CharacterWise);
            Assert.AreEqual("foo", backing.RegisterValue.StringValue);
        }
    }
}
