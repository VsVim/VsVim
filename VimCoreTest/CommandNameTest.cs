using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;
using VimCoreTest.Utils;

namespace VimCoreTest
{
    [TestFixture]
    public class CommandNameTest
    {
        private CommandName CreateOne(char c)
        {
            return CommandName.NewOneKeyInput(InputUtil.CharToKeyInput(c));
        }

        private CommandName CreateTwo(char c1, char c2)
        {
            return CommandName.NewTwoKeyInputs(InputUtil.CharToKeyInput(c1), InputUtil.CharToKeyInput(c2));
        }

        private CommandName CreateMany(params char[] all)
        {
            return CommandName.NewManyKeyInputs(all.Select(InputUtil.CharToKeyInput).ToFSharpList());
        }

        [Test]
        public void Add1()
        {
            var name1 = CommandName.NewOneKeyInput(InputUtil.CharToKeyInput('c'));
            var name2 = name1.Add(InputUtil.CharToKeyInput('a'));
            Assert.AreEqual("ca", name2.Name);
        }

        [Test]
        public void Name1()
        {
            var name1 = CommandName.NewOneKeyInput(InputUtil.CharToKeyInput('c'));
            Assert.AreEqual("c", name1.Name);
        }

        [Test]
        public void Equality()
        {
            EqualityUtil.RunAll(
                (left, right) => left == right,
                (left, right) => left != right,
                true,
                true,
                EqualityUnit.Create(CreateOne('a')).WithEqualValues(CreateOne('a')),
                EqualityUnit.Create(CreateOne('a')).WithNotEqualValues(CreateOne('b')),
                EqualityUnit.Create(CreateOne('a')).WithEqualValues(CreateMany('a')),
                EqualityUnit.Create(CreateOne('D')).WithEqualValues(CommandUtil.CreateCommandName("D")),
                EqualityUnit.Create(CommandName.NewOneKeyInput(InputUtil.CharToKeyInput('D'))).WithEqualValues(CommandUtil.CreateCommandName("D")));
        }
    }
}
