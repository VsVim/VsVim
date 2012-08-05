using System;
using System.Collections.Generic;
using System.Windows.Input;
using Moq;
using Vim.UI.Wpf.Implementation.Keyboard;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class KeyboardMapBuilderTest
    {
        internal KeyboardMapBuilder _builder;
        internal Dictionary<KeyState, VimKeyData> _keyStateToVimKeyDataMap;
        internal Dictionary<KeyInput, FrugalList<KeyState>> _keyInputToWpfKeyDataMap;

        private void Create()
        {
            _builder.Create(out _keyStateToVimKeyDataMap, out _keyInputToWpfKeyDataMap);
        }

        private void AssertMapping(KeyState keyState, string text, KeyModifiers modifiers = KeyModifiers.None)
        {
            VimKeyData vimKeyData;
            Assert.True(_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData));
            Assert.Equal(text, vimKeyData.TextOptional);
            Assert.Equal(modifiers, vimKeyData.KeyInputOptional.KeyModifiers);
        }

        public sealed class FakeKeyboardMapBuilderTest : KeyboardMapBuilderTest
        {
            private readonly MockVirtualKeyboard _mockVirtualKeyboard;

            public FakeKeyboardMapBuilderTest()
            {
                _mockVirtualKeyboard = new MockVirtualKeyboard();
                _builder = new KeyboardMapBuilder(_mockVirtualKeyboard);
            }

            /// <summary>
            /// Make sure that the code discovers that a caps lock + an alpha is a particular key 
            /// mapping for letters
            /// </summary>
            [Fact]
            public void CapsLockAndAlpha()
            {
                Create();
                AssertMapping(new KeyState(Key.A, VirtualKeyModifiers.CapsLock), "A", KeyModifiers.None);
                AssertMapping(new KeyState(Key.B, VirtualKeyModifiers.CapsLock), "B", KeyModifiers.None);
            }

            /// <summary>
            /// Make sure that both cases of the asterisks are properly handled (the number pad and the
            /// keypad)
            /// </summary>
            [Fact]
            public void BothAsterisks()
            {
                Create();
                AssertMapping(new KeyState(Key.Multiply, VirtualKeyModifiers.None), "*", KeyModifiers.None);
                AssertMapping(new KeyState(Key.D8, VirtualKeyModifiers.Shift), "*", KeyModifiers.None);
            }

            /// <summary>
            /// Make sure we can discover the case where it takes an OEM specific modifier to get an known
            /// character like slash.  The code must probe the keyboard looking for the OEM1 modifier key
            /// because there is no way to directly query for it 
            /// </summary>
            [Fact]
            public void OemModifier_Single()
            {
                var oem1VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.Oem102);
                _mockVirtualKeyboard.Oem1Modifier = oem1VirtualKey;
                _mockVirtualKeyboard.KeyMap[new KeyState(Key.Add, VirtualKeyModifiers.Oem1)] = "/";
                Create();

                AssertMapping(new KeyState(Key.Add, VirtualKeyModifiers.Oem1), "/");
                Assert.Equal(oem1VirtualKey, _mockVirtualKeyboard.KeyboardState.Oem1ModifierVirtualKey.Value);
            }

            /// <summary>
            /// Make sure we can discover the case where it takes an OEM specific modifier to get an known
            /// character like slash.  The code must probe the keyboard looking for the OEM1 modifier key
            /// because there is no way to directly query for it 
            /// </summary>
            [Fact]
            public void OemModifier_Single2()
            {
                var oem2VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.Oem102);
                _mockVirtualKeyboard.Oem2Modifier = oem2VirtualKey;
                _mockVirtualKeyboard.KeyMap[new KeyState(Key.Add, VirtualKeyModifiers.Oem2)] = "/";
                Create();

                AssertMapping(new KeyState(Key.Add, VirtualKeyModifiers.Oem2), "/");
                Assert.Equal(oem2VirtualKey, _mockVirtualKeyboard.KeyboardState.Oem2ModifierVirtualKey.Value);
            }
        }

        public sealed class RealKeyboardMapBuilderTest : KeyboardMapBuilderTest
        {
            private const string CharLettersLower = "abcdefghijklmnopqrstuvwxyz";

            public RealKeyboardMapBuilderTest()
            {
                var keyboardId = NativeMethods.GetKeyboardLayout(0);
                _builder = new KeyboardMapBuilder(new StandardVirtualKeyboard(keyboardId));
            }

            /// <summary>
            /// Ensure that the standard alpha mappings apply to this given keyboard layout
            /// </summary>
            [Fact]
            public void NormalAlpha()
            {
                Create();
                foreach (var cur in CharLettersLower)
                {
                    var upper = Char.ToUpper(cur).ToString();
                    var key = (Key)Enum.Parse(typeof(Key), upper);
                    AssertMapping(new KeyState(key, VirtualKeyModifiers.None), cur.ToString());
                    AssertMapping(new KeyState(key, VirtualKeyModifiers.Shift), upper);
                    AssertMapping(new KeyState(key, VirtualKeyModifiers.CapsLock), upper);
                }
            }
        }
    }
}
