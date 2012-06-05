using System;
using System.Windows.Input;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public sealed class KeyboardMapTest : IDisposable
    {
        private IntPtr _customId;
        private bool _mustUnloadLayout;
        private KeyboardMap _map;

        public KeyboardMapTest()
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
                _customId = NativeMethods.LoadKeyboardLayout(id, 0, out _mustUnloadLayout);
                Assert.NotEqual(IntPtr.Zero, _customId);
                _map = new KeyboardMap(_customId);
            }
        }

        public void Dispose()
        {
            if (_customId != IntPtr.Zero)
            {
                if (_mustUnloadLayout)
                {
                    Assert.True(NativeMethods.UnloadKeyboardLayout(_customId));
                }

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
            Assert.Equal(keyInput, _map.GetKeyInput(c, modifierKeys));
        }

        private KeyInput GetKeyInput(Key key)
        {
            KeyInput ki;
            Assert.True(_map.TryGetKeyInput(key, out ki));
            return ki;
        }

        private KeyInput GetKeyInput(Key key, ModifierKeys modKeys)
        {
            KeyInput ki;
            Assert.True(_map.TryGetKeyInput(key, modKeys, out ki));
            return ki;
        }

        [Fact]
        public void TryGetKeyInput1()
        {
            KeyInput ki = GetKeyInput(Key.F12);
            Assert.Equal(VimKey.F12, ki.Key);
            Assert.Equal(KeyModifiers.None, ki.KeyModifiers);
        }

        [Fact]
        public void TryGetKeyInput2()
        {
            KeyInput ki = GetKeyInput(Key.F12, ModifierKeys.Shift);
            Assert.Equal(VimKey.F12, ki.Key);
            Assert.Equal(KeyModifiers.Shift, ki.KeyModifiers);
        }

        [Fact]
        public void TryGetKeyInput3()
        {
            Setup(NativeMethods.LayoutPortuguese);
            KeyInput ki = GetKeyInput(Key.D8, ModifierKeys.Control | ModifierKeys.Alt);
            Assert.Equal('[', ki.Char);
        }

        [Fact]
        public void GetKeyInput_EnglishAlpha()
        {
            AssertGetKeyInput('a', 'a', ModifierKeys.None);
            AssertGetKeyInput('A', 'A', ModifierKeys.None);
            AssertGetKeyInput('A', 'A', ModifierKeys.Shift);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('a'), 'a', ModifierKeys.Control);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('A'), 'A', ModifierKeys.Control);
            AssertGetKeyInput(KeyInputUtil.CharWithControlToKeyInput('A'), 'A', ModifierKeys.Control | ModifierKeys.Shift);
        }

        [Fact]
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

        [Fact]
        public void GetKeyInput_EnglishAlternateKeys()
        {
            Action<VimKey, Key> verifyFunc = (vimKey, key) =>
            {
                KeyInput ki;
                Assert.True(_map.TryGetKeyInput(key, out ki));
                Assert.Equal(vimKey, ki.Key);
                Assert.True(_map.TryGetKeyInput(key, ModifierKeys.Control, out ki));
                Assert.Equal(vimKey, ki.Key);
                Assert.Equal(KeyModifiers.Control, ki.KeyModifiers);
            };

            verifyFunc(VimKey.Enter, Key.Enter);
            verifyFunc(VimKey.Tab, Key.Tab);
            verifyFunc(VimKey.Escape, Key.Escape);
        }

        [Fact]
        public void GetKeyInput_TurkishFAlpha()
        {
            Setup(NativeMethods.LayoutTurkishF);
            AssertGetKeyInput('a', 'a', ModifierKeys.None);
            AssertGetKeyInput('ö', 'ö', ModifierKeys.None);
        }

        [Fact]
        public void GetKeyInput_TurkishFSymbol()
        {
            Setup(NativeMethods.LayoutTurkishF);
            AssertGetKeyInput('<', '<', ModifierKeys.None);
            AssertGetKeyInput('>', '>', ModifierKeys.Shift);
        }

        /// <summary>
        /// Vim doesn't distinguish between a # and a Shift+# key.  Ensure that this logic holds up at 
        /// this layer
        /// </summary>
        [Fact]
        public void GetKeyInput_PoundWithShift()
        {
            Assert.Equal(KeyInputUtil.VimKeyToKeyInput(VimKey.Pound), _map.GetKeyInput('#', ModifierKeys.Shift));
        }

        [Fact]
        public void IsDeadKey_French_Accent()
        {
            Setup(NativeMethods.LayoutFrench);
            Assert.True(_map.IsDeadKey(Key.Oem6, ModifierKeys.None));
        }
    }
}
