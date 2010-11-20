using System;
using System.Windows.Input;
using NUnit.Framework;

namespace Vim.UI.Wpf.Test
{
    [TestFixture]
    public class KeyboardMapTest
    {
        private IntPtr _customId;
        private KeyboardMap _map;

        [SetUp]
        public void Setup()
        {
            Setup(null);
        }

        public void Setup(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                _customId = IntPtr.Zero;
                _map = new KeyboardMap(NativeMethods.GetKeyboardLayout(0));
            }
            else
            {
                _customId = NativeMethods.LoadKeyboardLayout(id, 0);
                Assert.AreNotEqual(IntPtr.Zero, _customId);
                _map = new KeyboardMap(_customId);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_customId != IntPtr.Zero)
            {
                NativeMethods.UnloadKeyboardLayout(_customId);
            }
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

        [Test]
        public void TryGetKeyInput3()
        {
            Setup(NativeMethods.LanguagePortuguese);
            KeyInput ki = GetKeyInput(Key.D8, ModifierKeys.Control | ModifierKeys.Alt);
            Assert.AreEqual('[', ki.Char);
        }


    }
}
