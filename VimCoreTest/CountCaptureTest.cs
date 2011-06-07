using System;
using Microsoft.FSharp.Core;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class CountCaptureTest
    {
        /// <summary>
        /// Get the BindResult created from playing this text to the function
        /// </summary>
        public Tuple<FSharpOption<int>, KeyInput> GetComplete(string text)
        {
            var first = KeyInputUtil.CharToKeyInput(text[0]);
            var result = CountCapture.GetCount(FSharpOption<KeyRemapMode>.None, first);
            if (text.Length > 1)
            {
                return result.Run(text.Substring(1)).AsComplete().Item;
            }

            return result.AsComplete().Item;
        }

        /// <summary>
        /// Letters don't count as a count
        /// </summary>
        [Test]
        public void GetCount_NoCount()
        {
            var tuple = GetComplete("a");
            Assert.IsTrue(tuple.Item1.IsNone());
            Assert.AreEqual(VimKey.LowerA, tuple.Item2.Key);
        }

        /// <summary>
        /// Zero is not actually a count it is instead a motion.
        /// </summary>
        [Test]
        public void GetCount_Zero()
        {
            var tuple = GetComplete("0");
            Assert.IsTrue(tuple.Item1.IsNone());
            Assert.AreEqual(VimKey.Number0, tuple.Item2.Key);
        }

        /// <summary>
        /// Test with a simple count followed by a value
        /// </summary>
        [Test]
        public void GetCount_Simple()
        {
            var tuple = GetComplete("42a");
            Assert.IsTrue(tuple.Item1.IsSome(42));
            Assert.AreEqual(VimKey.LowerA, tuple.Item2.Key);
        }
    }
}
