using System.Windows.Input;
using NUnit.Framework;
using Vim.UI.Wpf.Implementation;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyboardDeviceImplTest
    {
        private KeyboardDeviceImpl _deviceImpl;

        [SetUp]
        public void SetUp()
        {
            _deviceImpl = new KeyboardDeviceImpl();
        }

        [Test]
        [Description("Don't throw on the None case")]
        public void IsKeyDown1()
        {
            Assert.IsFalse(_deviceImpl.IsKeyDown(Key.None));
        }
    }
}
