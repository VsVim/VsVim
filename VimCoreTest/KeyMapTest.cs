using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;

namespace VimCoreTest
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

        [Test, Description("Having the source contain more than one key stroke is not supported")]
        public void MapWithNoRemap4()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("aaoue", "b", KeyRemapMode.Normal));
        }

        [Test, Description("Non-alpha-numerics are not supported yet")]
        public void MapWithNoRemap5()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("&", "!", KeyRemapMode.Normal));
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

        [Test,Description("Don't map the empty string")]
        public void MapWithNoRemap8()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("a", "", KeyRemapMode.Normal));
        }
    }
}
