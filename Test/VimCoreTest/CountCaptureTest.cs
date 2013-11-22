using System;
using Microsoft.FSharp.Core;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public class CountCaptureTest
    {
        /// <summary>
        /// Get the BindResult created from playing this text to the function
        /// </summary>
        public Tuple<FSharpOption<int>, KeyInput> GetComplete(string text)
        {
            var first = KeyInputUtil.CharToKeyInput(text[0]);
            var result = CountCapture.GetCount(KeyRemapMode.None, first);
            if (text.Length > 1)
            {
                return result.Run(text.Substring(1)).AsComplete().Item;
            }

            return result.AsComplete().Item;
        }

        /// <summary>
        /// Letters don't count as a count
        /// </summary>
        [Fact]
        public void GetCount_NoCount()
        {
            var tuple = GetComplete("a");
            Assert.True(tuple.Item1.IsNone());
            Assert.Equal(VimKey.RawCharacter, tuple.Item2.Key);
            Assert.Equal('a', tuple.Item2.Char);
        }

        /// <summary>
        /// Zero is not actually a count it is instead a motion.
        /// </summary>
        [Fact]
        public void GetCount_Zero()
        {
            var tuple = GetComplete("0");
            Assert.True(tuple.Item1.IsNone());
            Assert.Equal(VimKey.RawCharacter, tuple.Item2.Key);
            Assert.Equal('0', tuple.Item2.Char);
        }

        /// <summary>
        /// Test with a simple count followed by a value
        /// </summary>
        [Fact]
        public void GetCount_Simple()
        {
            var tuple = GetComplete("42a");
            Assert.True(tuple.Item1.IsSome(42));
            Assert.Equal(VimKey.RawCharacter, tuple.Item2.Key);
            Assert.Equal('a', tuple.Item2.Char);
        }
    }
}
