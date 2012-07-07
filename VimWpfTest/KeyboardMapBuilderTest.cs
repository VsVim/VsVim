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
            public RealKeyboardMapBuilderTest()
            {
                var keyboardId = NativeMethods.GetKeyboardLayout(0);
                _builder = new KeyboardMapBuilder(new StandardVirtualKeyboard(keyboardId));
            }

            /// <summary>
            /// Make sure that CTRL-D doesn't have a direct mapping here.  It actually maps to an unprintable character
            /// in the standard QWERTY keyboard.  One which we don't recognize in VsVim.  
            /// </summary>
            [Fact]
            public void ControlD()
            {
                Create();
                var keyState = new KeyState(Key.D, VirtualKeyModifiers.Control);
                Assert.False(_keyStateToVimKeyDataMap.ContainsKey(keyState));
            }

            /// <summary>
            /// The normal D though should be included in the mapping 
            /// </summary>
            [Fact]
            public void NormalD()
            {
                Create();
                AssertMapping(new KeyState(Key.D, VirtualKeyModifiers.None), "d");
                AssertMapping(new KeyState(Key.D, VirtualKeyModifiers.Shift), "D");
                AssertMapping(new KeyState(Key.D, VirtualKeyModifiers.CapsLock), "D");
            }
        }
    }
}
