using System;
using System.Linq;
using System.Windows.Input;
using Vim.UI.Wpf.Implementation.Keyboard;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class KeyboardMapTest
    {
        internal KeyboardMap _map;

        protected void AssertGetKeyInput(char c1, char c2, ModifierKeys modifierKeys)
        {
            AssertGetKeyInput(KeyInputUtil.CharToKeyInput(c1), c2, modifierKeys);
        }

        protected void AssertGetKeyInput(VimKey key, char c, ModifierKeys modifierKeys)
        {
            AssertGetKeyInput(KeyInputUtil.VimKeyToKeyInput(key), c, modifierKeys);
        }

        protected void AssertGetKeyInput(KeyInput keyInput, char c, ModifierKeys modifierKeys)
        {
            Assert.Equal(keyInput, KeyboardMap.GetKeyInput(c, modifierKeys));
        }

        protected KeyInput GetKeyInput(Key key)
        {
            KeyInput ki;
            Assert.True(_map.TryGetKeyInput(key, out ki));
            return ki;
        }

        protected KeyInput GetKeyInput(Key key, ModifierKeys modKeys)
        {
            KeyInput ki;
            Assert.True(_map.TryGetKeyInput(key, modKeys, out ki));
            return ki;
        }

        public sealed class FakeKeyboardTest : KeyboardMapTest
        {
            private readonly MockVirtualKeyboard _mockVirtualKeyboard;

            public FakeKeyboardTest()
            {
                _mockVirtualKeyboard = new MockVirtualKeyboard();
                _map = new KeyboardMap(IntPtr.Zero, _mockVirtualKeyboard);
            }

            /// <summary>
            /// When the caps lock is down we need to respect the data
            /// </summary>
            [Fact]
            public void CapsLockAndAlpha()
            {
                _mockVirtualKeyboard.IsCapsLockToggled = true;
                var keyInput = GetKeyInput(Key.A);
                Assert.Equal('A', keyInput.Char);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }

            /// <summary>
            /// Even with the caps lock down the digit keys should still be registering as
            /// a number and not the shifted state
            /// </summary>
            [Fact]
            public void CapsLockAndDigits()
            {
                _mockVirtualKeyboard.IsCapsLockToggled = true;
                var keyInput = GetKeyInput(Key.D1);
                Assert.Equal('1', keyInput.Char);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }

            /// <summary>
            /// Alpha is strange in that vim normalizes shift on them and won't put it for upper
            /// case characters
            /// </summary>
            [Fact]
            public void ShiftAndAlpha()
            {
                var keyInput = GetKeyInput(Key.B, ModifierKeys.Shift);
                Assert.Equal('B', keyInput.Char);
                Assert.Equal(KeyModifiers.None, keyInput.KeyModifiers);
            }

            /// <summary>
            /// We only look for the control + key combinations for keys that are standard in vim.  If 
            /// we see a key that has a non-control binding and control is specified then we should just
            /// apply Control to the KeyInput.  The Vim documentation is ambiguous here and actually
            /// refers to trying this but it not being reliable. 
            /// </summary>
            [Fact]
            public void UnrecognizedControl()
            {
                // Make sure that we are dealing with a case where the non-control is present but the
                // control version isn't
                Assert.False(_mockVirtualKeyboard.KeyMap.ContainsKey(new KeyState(Key.Escape, ModifierKeys.Control)));

                var keyInput = GetKeyInput(Key.Escape, ModifierKeys.Control);
                Assert.Equal(KeyModifiers.Control, keyInput.KeyModifiers);
                Assert.Equal(VimKey.Escape, keyInput.Key);
            }
        }

        public sealed class RealKeyboardTest : KeyboardMapTest, IDisposable
        {
            private IntPtr _customId;
            private bool _mustUnloadLayout;

            public RealKeyboardTest()
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
                };

                verifyFunc(VimKey.Enter, Key.Enter);
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

            [Fact]
            public void IsDeadKey_French_Accent()
            {
                Setup(NativeMethods.LayoutFrench);
                Assert.True(_map.IsDeadKey(Key.Oem6, ModifierKeys.None));
            }

            /// <summary>
            /// Internally in the KeyboardMapBuilder we don't maintain a mapping to CTRL-D because it doesn't
            /// map to a character that we reconize as valid (CTRL-D maps to (char)0x4 which isn't a printable
            /// character).  
            ///
            /// But KeyboardMap is the public facing API.  It needs to produce a value when CTRL-D is pressed 
            /// by the user
            /// </summary>
            [Fact]
            public void ControlAlpha()
            {
                var baseCharacter = (int)'a';
                var baseKey = (int)Key.A;
                foreach (var i in Enumerable.Range(0, 26))
                {
                    var letter = (char)(baseCharacter + i);
                    var expected = KeyInputUtil.CharWithControlToKeyInput(letter);

                    var key = (Key)(baseKey + i);
                    var found = GetKeyInput(key, ModifierKeys.Control);
                    Assert.Equal(expected, found);

                    var notation = String.Format("<C-{0}>", letter);
                    var mapped = KeyNotationUtil.StringToKeyInput(notation);
                    Assert.Equal(expected, mapped);
                }
            }
        }
    }
}
