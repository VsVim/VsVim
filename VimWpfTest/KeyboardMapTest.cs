using System;
using System.Windows.Input;
using NUnit.Framework;

namespace Vim.UI.Wpf.UnitTest
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
                Assert.IsTrue(NativeMethods.UnloadKeyboardLayout(_customId));
                NativeMethods.LoadKeyboardLayout(NativeMethods.LayoutEnglish, NativeMethods.KLF_ACTIVATE);
            }
            _customId = IntPtr.Zero;
        }

        private void AssertGetKeyInput(char c1, char c2, ModifierKeys modifierKeys)
        {
            AssertGetKeyInput(KeyInputUtil.CharToKeyInput(c1), c2, modifierKeys);
        }

        private void AssertGetKeyInput(VimKey key, char c, ModifierKeys modifierKeys)
        {
            AssertGetKeyInput(KeyInputUtil.VimKeyToKeyInput(key), c, modifierKeys);
        }

        private void AssertGetKeyInput(KeyInput keyInput, char c, ModifierKeys modifierKeys)
        {
            Assert.AreEqual(keyInput, _map.GetKeyInput(c, modifierKeys));
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
            Setup(NativeMethods.LayoutPortuguese);
            KeyInput ki = GetKeyInput(Key.D8, ModifierKeys.Control | ModifierKeys.Alt);
            Assert.AreEqual('[', ki.Char);
        }

        [Test]
        public void GetKeyInput_EnglishAlpha()
        {
            AssertGetKeyInput('a', 'a', ModifierKeys.None);
            AssertGetKeyInput('A', 'A', ModifierKeys.None);
            AssertGetKeyInput('A', 'A', ModifierKeys.Shift);
            AssertGetKeyInput(KeyInputUtil.CharWithShiftToKeyInput('a'), 'a', ModifierKeys.Shift);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('a'), 'a', ModifierKeys.Control);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('A'), 'A', ModifierKeys.Control);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('A'), 'A', ModifierKeys.Control | ModifierKeys.Shift);
        }

        [Test]
        public void GetKeyInput_EnglishSymbol()
        {
            var list = "!@#$%^&*()";
            foreach (var cur in list)
            {
                AssertGetKeyInput(cur, cur, ModifierKeys.None);
                AssertGetKeyInput(cur, cur, ModifierKeys.Shift);
                AssertGetKeyInput(cur, cur, ModifierKeys.Shift);
                AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput(cur), cur, ModifierKeys.Control | ModifierKeys.Shift);
            }
        }

        [Test]
        public void GetKeyInput_EnglishAlternateKeys()
        {
            Action<VimKey, Key> verifyFunc = (vimKey, key) =>
            {
                KeyInput ki;
                Assert.IsTrue(_map.TryGetKeyInput(key, out ki));
                Assert.AreEqual(vimKey, ki.Key);
                Assert.IsTrue(_map.TryGetKeyInput(key, ModifierKeys.Control, out ki));
                Assert.AreEqual(vimKey, ki.Key);
                Assert.AreEqual(KeyModifiers.Control, ki.KeyModifiers);
            };

            verifyFunc(VimKey.Enter, Key.Enter);
            verifyFunc(VimKey.Tab, Key.Tab);
            verifyFunc(VimKey.Escape, Key.Escape);
        }

        [Test]
        public void GetKeyInput_TurkishFAlpha()
        {
            Setup(NativeMethods.LayoutTurkishF);
            AssertGetKeyInput('a', 'a', ModifierKeys.None);
            AssertGetKeyInput('ö', 'ö', ModifierKeys.None);
        }

        [Test]
        public void GetKeyInput_TurkishFSymbol()
        {
            Setup(NativeMethods.LayoutTurkishF);
            AssertGetKeyInput('<', '<', ModifierKeys.None);
            AssertGetKeyInput('>', '>', ModifierKeys.Shift);
        }

    }
}
