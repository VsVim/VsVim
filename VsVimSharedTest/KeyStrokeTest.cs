using Xunit;
using Vim;
using Vim.UnitTest;

namespace VsVim.UnitTest
{
    public sealed class KeyStrokeTest
    {
        [Fact]
        public void Constructor_WithNoModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.None);
            Assert.Equal(KeyInputUtil.CharToKeyInput('c'), stroke.KeyInput);
            Assert.Equal(KeyInputUtil.CharToKeyInput('c'), stroke.AggregateKeyInput);
            Assert.Equal('c', stroke.Char);
        }

        [Fact]
        public void Constructor_WithShiftModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('#'),
                KeyModifiers.Shift);
            Assert.Equal(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
            Assert.Equal(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Pound, KeyModifiers.Shift), stroke.AggregateKeyInput);
            Assert.Equal('#', stroke.Char);
        }

        [Fact]
        public void KeyStroke_WithShiftAndControlModifier()
        {
            var stroke = new KeyStroke(
                KeyInputUtil.CharToKeyInput('#'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.Equal(KeyInputUtil.CharToKeyInput('#'), stroke.KeyInput);
            Assert.Equal(KeyInputUtil.ApplyModifiersToVimKey(VimKey.Pound, KeyModifiers.Shift | KeyModifiers.Control), stroke.AggregateKeyInput);
            Assert.Equal('#', stroke.Char);
        }

        [Fact]
        public void Equals1()
        {
            var stroke1 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            var stroke2 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.Equal(stroke1, stroke2);
            Assert.True(stroke1 == stroke2);
            Assert.False(stroke1 != stroke2);
        }

        [Fact]
        public void Equals2()
        {
            var stroke1 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('d'),
                KeyModifiers.Shift | KeyModifiers.Control);
            var stroke2 = new KeyStroke(
                KeyInputUtil.CharToKeyInput('c'),
                KeyModifiers.Shift | KeyModifiers.Control);
            Assert.NotEqual(stroke1, stroke2);
            Assert.False(stroke1 == stroke2);
            Assert.True(stroke1 != stroke2);
        }

        [Fact]
        public void Equals3()
        {
            var value = EqualityUnit
                .Create(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.None))
                .WithEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.None))
                .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('d'), KeyModifiers.None))
                .WithNotEqualValues(new KeyStroke(KeyInputUtil.CharToKeyInput('c'), KeyModifiers.Shift));
            EqualityUtil.RunAll(
                (x, y) => x == y,
                (x, y) => x != y,
                values: value);
        }
    }
}
