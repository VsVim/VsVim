using System;
using System.Windows.Input;
using Vim.UI.Wpf.Implementation.Misc;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    public abstract class AlternateKeyUtilTest
    {
        internal AlternateKeyUtil _keyUtilRaw;
        internal IKeyUtil _keyUtil;

        protected AlternateKeyUtilTest()
        {
            _keyUtilRaw = new AlternateKeyUtil();
            _keyUtil = _keyUtilRaw;
        }

        public sealed class SpecialToKeyInputTest : AlternateKeyUtilTest
        {
            private void AssertMap(Key key, ModifierKeys modifierKeys, KeyInput keyInput)
            {
                KeyInput mapped;
                Assert.True(_keyUtil.TryConvertSpecialToKeyInput(key, modifierKeys, out mapped));
                Assert.Equal(keyInput, mapped);
            }

            private void AssertMap(Key key, char c)
            {
                AssertMap(key, ModifierKeys.Control, KeyInputUtil.CharWithControlToKeyInput(c));
            }

            private void AssertMap(Key key, VimKey vimKey)
            {
                AssertMap(key, ModifierKeys.Control, KeyInputUtil.ApplyModifiersToVimKey(vimKey, KeyModifiers.Control));
            }

            [Fact]
            public void ArrowKeys()
            {
                AssertMap(Key.Left, VimKey.Left);
                AssertMap(Key.Up, VimKey.Up);
                AssertMap(Key.Right, VimKey.Right);
                AssertMap(Key.Down, VimKey.Down);
            }

            [Fact]
            public void FunctionKeys()
            {
                for (int i = 1; i <= 12; i++)
                {
                    var name = string.Format("F{0}", i);
                    var key = (Key)Enum.Parse(typeof(Key), name);
                    var vimKey = (VimKey)Enum.Parse(typeof(VimKey), name);
                    AssertMap(key, vimKey);
                }
            }

            [Fact]
            public void Special()
            {
                AssertMap(Key.Space, ' ');
                AssertMap(Key.Tab, '\t');
                AssertMap(Key.Escape, VimKey.Escape);
                AssertMap(Key.Insert, VimKey.Insert);
                AssertMap(Key.Back, VimKey.Back);
                AssertMap(Key.Help, VimKey.Help);
                AssertMap(Key.Delete, VimKey.Delete);
                AssertMap(Key.Home, VimKey.Home);
                AssertMap(Key.End, VimKey.End);
            }

            /// <summary>
            /// Several keys have multiple names, make sure they both map
            /// </summary>
            [Fact]
            public void DoubleMapping()
            {
                AssertMap(Key.PageUp, VimKey.PageUp);
                AssertMap(Key.Prior, VimKey.PageUp);
                AssertMap(Key.PageDown, VimKey.PageDown);
                AssertMap(Key.Next, VimKey.PageDown);
            }

            [Fact]
            public void KeypadSpecial()
            {
                AssertMap(Key.Add, VimKey.KeypadPlus);
                AssertMap(Key.Subtract, VimKey.KeypadMinus);
                AssertMap(Key.Multiply, VimKey.KeypadMultiply);
                AssertMap(Key.Divide, VimKey.KeypadDivide);
                AssertMap(Key.Separator, VimKey.KeypadEnter);
                AssertMap(Key.Decimal, VimKey.KeypadDecimal);
            }

            /// <summary>
            /// Make sure that we special case the space key.  It's one of the virtual keys
            /// that we handle here
            /// </summary>
            [Fact]
            public void Issue977()
            {
                AssertMap(Key.Space, ' ');
            }
        }
    }
}
