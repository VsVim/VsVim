using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for TextUtilTest2
    /// </summary>
    [TestFixture]
    public class TextUtilTest2
    {
        public string FindNextWord(string input, int index)
        {
            return TextUtil.FindNextWord(WordKind.NormalWord, input, index);
        }

        [Test]
        public void FindNextWord1()
        {
            Assert.AreEqual("foo", FindNextWord("bar foo", 0));
        }
    }
}
