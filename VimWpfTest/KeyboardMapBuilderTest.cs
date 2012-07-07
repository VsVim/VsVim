using System.Collections.Generic;
using System.Windows.Input;
using Vim.UI.Wpf.Implementation.Keyboard;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public sealed class KeyboardMapBuilderTest
    {
        private readonly MockVirtualKeyboard _mockVirtualKeyboard;
        private readonly KeyboardMapBuilder _builder;
        private Dictionary<KeyState, VimKeyData> _keyStateToVimKeyDataMap;
        private Dictionary<KeyInput, FrugalList<KeyState>> _keyInputToWpfKeyDataMap;

        public KeyboardMapBuilderTest()
        {
            _mockVirtualKeyboard = new MockVirtualKeyboard();
            _builder = new KeyboardMapBuilder(_mockVirtualKeyboard);
        }

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
}
