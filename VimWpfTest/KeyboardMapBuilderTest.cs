using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.UI.Wpf.Implementation.Keyboard;
using Xunit;
using System.Windows.Input;

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

        /// <summary>
        /// Make sure that the code discovers that a caps lock + an alpha is a particular key 
        /// mapping for letters
        /// </summary>
        [Fact]
        public void CapsLockAndAlpha()
        {
            Create();
            VimKeyData vimKeyData;
            Assert.True(_keyStateToVimKeyDataMap.TryGetValue(new KeyState(Key.A, VirtualKeyModifiers.CapsLock), out vimKeyData));
            Assert.Equal("A", vimKeyData.TextOptional);
        }
    }
}
