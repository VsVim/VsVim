using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Vim;
using Microsoft.VisualStudio.Text;
using Microsoft.FSharp.Core;

namespace VimCoreTest
{
    /// <summary>
    /// Summary description for TextUtilTest
    /// </summary>
    [TestFixture]
    public class TextUtilTest
    {
        string FindCurrentNormalWord(string input, int index)
        {
            return TextUtil.FindCurrentWord(WordKind.NormalWord, input, index);
        }

        string FindCurrentBigWord(string input, int index)
        {
            return TextUtil.FindCurrentWord(WordKind.BigWord, input, index);
        }
        /// <summary>
        /// Basic tests
        /// </summary>
        [Test]
        public void FindWord1()
        {
            Assert.AreEqual("foo",FindCurrentNormalWord("foo ", 0));
            Assert.AreEqual("foo_123",FindCurrentNormalWord("foo_123", 0));
        }

        /// <summary>
        /// Non-zero index tests
        /// </summary>
        [Test]
        public void FindWord2()
        {
            Assert.AreEqual("oo",FindCurrentNormalWord("foo", 1));
            Assert.AreEqual("oo123",FindCurrentNormalWord("foo123", 1));
        }

        /// <summary>
        /// Limits
        /// </summary>
        [Test]
        public void FindWord3()
        {
            Assert.AreEqual("",FindCurrentNormalWord(" foo", 0));
            Assert.AreEqual("oo_",FindCurrentNormalWord("foo_", 1));
            Assert.AreEqual("",FindCurrentNormalWord("foo", 23));
        }

        /// <summary>
        /// Non-keyword words
        /// </summary>
        [Test]
        public void FindWord4()
        {
            Assert.AreEqual("!@#$",FindCurrentNormalWord("!@#$", 0));
            Assert.AreEqual("!!!",FindCurrentNormalWord("foo!!!", 3));
        }

        /// <summary>
        /// Mix of keyword and non-keyword strings
        /// </summary>
        [Test]
        public void FindWord5()
        {
            Assert.AreEqual("#$",FindCurrentNormalWord("#$foo", 0));
            Assert.AreEqual("foo",FindCurrentNormalWord("foo!@#$", 0));
        }

        [Test]
        public void FindBigWord1()
        {
            Assert.AreEqual("foo!@#$", FindCurrentBigWord("foo!@#$", 0));
            Assert.AreEqual("!foo!", FindCurrentBigWord("!foo!", 0));
        }

        [Test]
        public void FindFullWord1()
        {
            Assert.AreEqual("foo", TextUtil.FindFullWord(WordKind.BigWord, "foo", 0));
            Assert.AreEqual("foo", TextUtil.FindFullWord(WordKind.BigWord, "foo", 1));
            Assert.AreEqual("foo", TextUtil.FindFullWord(WordKind.BigWord, "foo", 2));
            Assert.AreEqual("foo_123", TextUtil.FindFullWord(WordKind.BigWord, "foo_123", 2));
        }

        [Test]
        public void FindFullWord2()
        {
            Assert.AreEqual("foo", TextUtil.FindFullWord(WordKind.NormalWord,"foo bar", 2));
            Assert.AreEqual("bar", TextUtil.FindFullWord(WordKind.NormalWord,"foo bar", 5));
            Assert.AreEqual("", TextUtil.FindFullWord(WordKind.NormalWord,"foo bar", 3));
        }

        [Test]
        public void FindFullBigWord1()
        {
            Assert.AreEqual("!@#", TextUtil.FindFullWord(WordKind.BigWord,"!@#", 0));
            Assert.AreEqual("!@#", TextUtil.FindFullWord(WordKind.BigWord,"!@#", 1));
            Assert.AreEqual("!@#", TextUtil.FindFullWord(WordKind.BigWord, "!@#", 2));
        }

        [Test]
        public void FindPreviousWordStart1()
        {
            Assert.AreEqual(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord,"foo", 1).Value.Start);
            Assert.AreEqual(0, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 2).Value.Start);
        }

        /// <summary>
        /// Move back accross a blank
        /// </summary>
        [Test]
        public void FindPreviousWordStart2()
        {
            Assert.AreEqual("foo", TextUtil.FindPreviousWord(WordKind.NormalWord,"foo bar", 3));
            Assert.AreEqual("bar", TextUtil.FindPreviousWord(WordKind.NormalWord, "foo bar baz", 7));
        }

        /// <summary>
        /// Move back when starting at a word.  Shouldd go to the start of the previous word
        /// </summary>
        [Test]
        public void FindPreviousWordStart3()
        {
            Assert.AreEqual("foo", TextUtil.FindPreviousWord(WordKind.BigWord, "foo bar", 4));
        }

        /// <summary>
        /// At the start of a line there is no previous word 
        /// </summary>
        [Test]
        public void FindPreviousWordStart4()
        {
            Assert.AreEqual(FSharpOption<Span>.None, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "foo", 0));
            Assert.AreEqual(FSharpOption<Span>.None, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 1));
            Assert.AreEqual(FSharpOption<Span>.None, TextUtil.FindPreviousWordSpan(WordKind.NormalWord, "   foo", 3));
        }

        /// <summary>
        /// Mix of word and WORD characters
        /// </summary>
        [Test]
        public void FindPreviousWordStart5()
        {
            Assert.AreEqual("#$", TextUtil.FindPreviousWord(WordKind.NormalWord, "foo#$", 4));
            Assert.AreEqual("#$", TextUtil.FindPreviousWord(WordKind.NormalWord, "foo #$", 5));
        }

        [Test, Description("Simple find next word")]
        public void FindNextWord()
        {
            var res = TextUtil.FindNextWord(WordKind.NormalWord, "foo bar", 0);
            Assert.AreEqual("bar", res);
        }
    }
}
