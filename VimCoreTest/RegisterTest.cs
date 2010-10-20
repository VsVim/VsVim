using NUnit.Framework;
using Vim;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class RegisterTest
    {
        [Test]
        public void ValueBackingTest1()
        {
            var backing = new MockRegisterValueBacking();
            var reg = new Register(RegisterName.Unnamed, backing);
            reg.Value = new RegisterValue(StringData.NewSimple("foo"), MotionKind.Inclusive, OperationKind.CharacterWise);
            Assert.AreEqual("foo", backing.Value.Value.String);
        }

        [Test]
        public void ValueBackingTest2()
        {
            var backing = new MockRegisterValueBacking();
            var reg = new Register(RegisterName.Unnamed, backing);
            backing.Value = new RegisterValue(StringData.NewSimple("foo"), MotionKind.Inclusive, OperationKind.CharacterWise);
            Assert.AreEqual("foo", reg.StringValue);
        }
    }
}
