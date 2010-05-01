using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;

namespace VimCore.Test
{
    [TestFixture]
    public class KeyMapTest
    {
        [Test]
        public void MapWithNoRemap1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).Single();
            Assert.AreEqual(InputUtil.CharToKeyInput('b'), ret);
        }

        [Test]
        public void MapWithNoRemap2()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "1", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).Single();
            Assert.AreEqual(InputUtil.CharToKeyInput('1'), ret);
        }

        [Test]
        public void MapWithNoRemap4()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("aaoue", "b", KeyRemapMode.Normal));
        }

        [Test]
        public void MapWithNoRemap5()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("&", "!", KeyRemapMode.Normal));
        }

        [Test]
        public void MapWithNoRemap6()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
            Assert.AreEqual(2, ret.Count);
            Assert.AreEqual('b', ret[0].Char);
            Assert.AreEqual('c', ret[1].Char);
        }

        [Test]
        public void MapWithNoRemap7()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "bcd", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
            Assert.AreEqual(3, ret.Count);
            Assert.AreEqual('b', ret[0].Char);
            Assert.AreEqual('c', ret[1].Char);
            Assert.AreEqual('d', ret[2].Char);
        }

        [Test, Description("Don't map the empty string")]
        public void MapWithNoRemap8()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("a", "", KeyRemapMode.Normal));
        }

        [Test]
        public void MapWithRemap1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithRemap("a", "b", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).Single();
            Assert.AreEqual('b', ret.Char);
        }

        [Test]
        public void MapWithRemap2()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "bcd", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
            Assert.AreEqual(3, ret.Count);
            Assert.AreEqual('b', ret[0].Char);
            Assert.AreEqual('c', ret[1].Char);
            Assert.AreEqual('d', ret[2].Char);
        }

        [Test]
        public void MapWithRemap3()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithRemap("b", "c", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).Single();
            Assert.AreEqual('c', ret.Char);
        }

        [Test]
        public void MapWithRemap4()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithRemap("a", "bc", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithRemap("b", "d", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).ToList();
            Assert.AreEqual(2, ret.Count);
            Assert.AreEqual('d', ret[0].Char);
            Assert.AreEqual('c', ret[1].Char);
        }

        [Test, Description("Recursive mappings should not follow the recursion here")]
        public void GetKeyMapping1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithRemap("b", "a", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).Single();
            Assert.AreEqual('b', ret.Char);
        }

        [Test]
        public void GetKeyMappingResult1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithRemap("b", "a", KeyRemapMode.Normal));
            var ret = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
            Assert.IsTrue(ret.IsRecursiveMapping);
        }

        [Test]
        public void GetKeyMappingResult2()
        {
            var map = new KeyMap();
            var ret = map.GetKeyMappingResult(InputUtil.CharToKeyInput('b'), KeyRemapMode.Normal);
            Assert.IsTrue(ret.IsNoMapping);
        }

        [Test]
        public void GetKeyMapppingResult3()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            var res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
            Assert.IsTrue(res.IsSingleKey);
            Assert.AreEqual('b', res.AsSingleKey().item.Char);
        }

        [Test]
        public void GetKeyMappingResult4()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "bc", KeyRemapMode.Normal));
            var res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
            Assert.IsTrue(res.IsKeySequence);
            var list = res.AsKeySequence().Item.ToList();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual('b', list[0].Char);
            Assert.AreEqual('c', list[1].Char);
        }

        [Test]
        public void GetKeyMappingResult5()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal));
            var res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
            Assert.IsTrue(res.IsMappingNeedsMoreInput);
        }

        [Test]
        public void Clear1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            map.Clear(KeyRemapMode.Normal);
            Assert.IsTrue(map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsNoMapping);
        }

        [Test, Description("Only clear the specified mode")]
        public void Clear2()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
            map.Clear(KeyRemapMode.Normal);
            var res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Insert);
            Assert.IsTrue(res.IsSingleKey);
            Assert.AreEqual('b', res.AsSingleKey().Item.Char);
        }

        [Test]
        public void ClearAll()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Insert));
            map.ClearAll();
            var res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Insert);
            Assert.IsTrue(res.IsNoMapping);
            res = map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal);
            Assert.IsTrue(res.IsNoMapping);

        }

        [Test]
        public void Unmap1()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsTrue(map.Unmap("a", KeyRemapMode.Normal));
            Assert.IsTrue(map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsNoMapping);
        }

        [Test]
        public void Unmap2()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "b", KeyRemapMode.Normal));
            Assert.IsFalse(map.Unmap("a", KeyRemapMode.Insert));
            Assert.IsTrue(map.GetKeyMappingResult(InputUtil.CharToKeyInput('a'), KeyRemapMode.Normal).IsSingleKey);
        }

        [Test]
        public void GetKeyMappingResultFromMultiple1()
        {
            IKeyMap map = new KeyMap();
            map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

            var input = "aa".Select(InputUtil.CharToKeyInput);
            var res = map.GetKeyMappingResultFromMultiple(input, KeyRemapMode.Normal);
            Assert.IsTrue(res.IsSingleKey);
            Assert.AreEqual('b', res.AsSingleKey().Item.Char);
        }

        [Test]
        public void GetKeyMappingResultFromMultiple2()
        {
            IKeyMap map = new KeyMap();
            map.MapWithNoRemap("aa", "b", KeyRemapMode.Normal);

            var input = "a".Select(InputUtil.CharToKeyInput);
            var res = map.GetKeyMappingResultFromMultiple(input, KeyRemapMode.Normal);
            Assert.IsTrue(res.IsMappingNeedsMoreInput);
        }
    }
}
