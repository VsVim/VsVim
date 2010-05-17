using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim.Modes.Command;
using Vim;
using Microsoft.FSharp.Collections;

namespace VimCore.Test
{
    [TestFixture]
    public class CommandParseUtilTest
    {
        private static string ToString(IEnumerable<KeyInput> list)
        {
            return new String(list.Select(x => x.Char).ToArray());
        }

        [Test]
        public void ParseKeyRemapOptions1()
        {
            var tuple = CommandParseUtil.ParseKeyRemapOptions(ListModule.OfSeq("<buffer> baz"));
            Assert.AreEqual(KeyRemapOptions.Buffer, tuple.Item2);
        }

        [Test]
        public void ParseKeyRemapOptions2()
        {
            var tuple = CommandParseUtil.ParseKeyRemapOptions(ListModule.OfSeq("a"));
            Assert.AreEqual(KeyRemapOptions.None, tuple.Item2);
            Assert.AreEqual('a', tuple.Item1.First());
        }
    }
}
