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
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('b'), ret.Value.Item1);
        }

        [Test]
        public void MapWithNoRemap2()
        {
            var map = new KeyMap();
            Assert.IsTrue(map.MapWithNoRemap("a", "1", KeyRemapMode.Normal));
            var ret = map.GetKeyMapping(InputUtil.CharToKeyInput('a'));
            Assert.IsTrue(ret.IsSome());
            Assert.AreEqual(InputUtil.CharToKeyInput('1'), ret.Value.Item1);
        }

        [Test, Description("Non-alpha-numerics are not supported yet")]
        public void MapWithNoRemap4()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("aaoue", "b", KeyRemapMode.Normal));
        }

        [Test, Description("Non-alpha-numerics are not supported yet")]
        public void MapWithNoRemap5()
        {
            var map = new KeyMap();
            Assert.IsFalse(map.MapWithNoRemap("a", "aoeu", KeyRemapMode.Normal));
        }
    }
}
