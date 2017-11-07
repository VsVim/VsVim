using System.Windows.Input;
using Xunit;
using Vim.UI.Wpf.Implementation.Misc;
using Vim.UnitTest;

namespace Vim.UI.Wpf.UnitTest
{
    public class KeyboardDeviceImplTest
    {
        private readonly KeyboardDeviceImpl _deviceImpl;

        public KeyboardDeviceImplTest()
        {
            _deviceImpl = new KeyboardDeviceImpl(new AlternateKeyUtil());
        }

        /// <summary>
        /// Don't throw on the None case
        /// </summary>
        [WpfFact]
        public void IsKeyDown1()
        {
            Assert.False(_deviceImpl.IsKeyDown(Key.None));
        }
    }
}
