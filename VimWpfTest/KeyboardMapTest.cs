using System.Windows.Input;
using NUnit.Framework;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyboardMapTest
    {
        private KeyboardMap _map;

        [SetUp]
        public void Setup()
        {
            _map = new KeyboardMap(NativeMethods.GetKeyboardLayout(0));
        }

        private KeyInput GetKeyInput(Key key)
        {
            KeyInput ki;
            Assert.IsTrue(_map.TryGetKeyInput(key, out ki));
            return ki;
        }

        private KeyInput GetKeyInput(Key key, ModifierKeys modKeys)
        {
            KeyInput ki;
            Assert.IsTrue(_map.TryGetKeyInput(key, modKeys, out ki));
            return ki;
        }

        [Test]
        public void TryGetKeyInput1()
        {
            KeyInput ki = GetKeyInput(Key.F12);
            Assert.AreEqual(VimKey.F12, ki.Key);
            Assert.AreEqual(KeyModifiers.None, ki.KeyModifiers);
        }

        [Test]
        public void TryGetKeyInput2()
        {
            KeyInput ki = GetKeyInput(Key.F12, ModifierKeys.Shift);
            Assert.AreEqual(VimKey.F12, ki.Key);
            Assert.AreEqual(KeyModifiers.Shift, ki.KeyModifiers);
        }
    }
}
