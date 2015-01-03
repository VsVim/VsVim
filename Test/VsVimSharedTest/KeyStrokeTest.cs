using Vim;
using Vim.UnitTest;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class KeyStrokeTest
    {
        public sealed class ConstructorTest : KeyStrokeTest
        {
            [Fact]
            public void WithNoModifier()
            {
                var stroke = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('c'),
                    VimKeyModifiers.None);
                Assert.Equal(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
                Assert.Equal(KeyInputUtil.CharToKeyInput('c'), stroke.AggregateKeyInput);
                Assert.Equal('c', stroke.Char);
            }

            [Fact]
            public void WithShiftModifier()
            {
                var stroke = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('#'),
                    VimKeyModifiers.Shift);
                Assert.Equal(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
                Assert.Equal(KeyInputUtil.ApplyKeyModifiersToChar('#', VimKeyModifiers.Shift), stroke.AggregateKeyInput);
                Assert.Equal('#', stroke.Char);
            }
        }

        public sealed class MiscTest : KeyStrokeTest
        {
            [Fact]
            public void WithShiftAndControlModifier()
            {
                var stroke = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('#'),
                    VimKeyModifiers.Shift | VimKeyModifiers.Control);
                Assert.Equal(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
                Assert.Equal(KeyInputUtil.ApplyKeyModifiersToChar('#', VimKeyModifiers.Shift | VimKeyModifiers.Control),
                             stroke.AggregateKeyInput);
                Assert.Equal('#', stroke.Char);
            }

            [Fact]
            public void Equals1()
            {
                var stroke1 = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('c'),
                    VimKeyModifiers.Shift | VimKeyModifiers.Control);
                var stroke2 = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('c'),
                    VimKeyModifiers.Shift | VimKeyModifiers.Control);
                Assert.Equal(stroke1, stroke2);
                Assert.True(stroke1 == stroke2);
                Assert.False(stroke1 != stroke2);
            }

            [Fact]
            public void Equals2()
            {
                var stroke1 = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('d'),
                    VimKeyModifiers.Shift | VimKeyModifiers.Control);
                var stroke2 = new KeyStroke(
                    KeyInputUtil.CharToKeyInput('c'),
                    VimKeyModifiers.Shift | VimKeyModifiers.Control);
                Assert.NotEqual(stroke1, stroke2);
                Assert.False(stroke1 == stroke2);
                Assert.True(stroke1 != stroke2);
            }

            [Fact]
            public void Equals3()
            {
                var value = EqualityUnit
                    .Create(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), VimKeyModifiers.None))
                    .WithEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), VimKeyModifiers.None))
                    .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('d'), VimKeyModifiers.None))
                    .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), VimKeyModifiers.Shift));
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    values: value);
            }
        }
    }
}
