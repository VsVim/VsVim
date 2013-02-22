using Vim.UnitTest.Mock;
using Xunit;

namespace Vim.UnitTest
{
    public class RegisterTest
    {
        [Fact]
        public void ValueBackingTest1()
        {
            var backing = new MockRegisterValueBacking();
            var reg = new Register(RegisterName.Unnamed, backing);
            reg.RegisterValue = new RegisterValue("foo", OperationKind.CharacterWise);
            Assert.Equal("foo", backing.RegisterValue.StringValue);
        }
    }
}
