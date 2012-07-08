using System;
using System.Collections.Generic;
using System.Windows.Input;
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
            /// Make sure that all of the alpha characters map to a version with control pressed 
            /// that isn't the alpha character.  These are all defined in the ASCII standard and 
            /// listed in the VIM FAQ
            /// 
            /// http://vimhelp.appspot.com/vim_faq.txt.html#faq-20.5
            /// </summary>
            [Fact]
            public void ControlWithAlpha()
            {
                Create();
                foreach (var cur in CharLettersLower)
                {
                    var key = (Key)Enum.Parse(typeof(Key), Char.ToUpper(cur).ToString());
                    var keyState = new KeyState(key, ModifierKeys.Control);

                    VimKeyData vimKeyData;
                    Assert.True(_keyStateToVimKeyDataMap.TryGetValue(keyState, out vimKeyData));
                    Assert.Equal(KeyInputUtil.CharWithControlToKeyInput(cur), vimKeyData.KeyInputOptional);
                }
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
