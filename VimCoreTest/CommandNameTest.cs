using System.Linq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using VimCore.Test.Utils;

namespace VimCore.Test
{
    [TestFixture]
    public class CommandNameTest
    {
        private KeyInputSet CreateOne(char c)
        {
            return KeyInputSet.NewOneKeyInput(InputUtil.CharToKeyInput(c));
        }

        private KeyInputSet CreateTwo(char c1, char c2)
        {
            return KeyInputSet.NewTwoKeyInputs(InputUtil.CharToKeyInput(c1), InputUtil.CharToKeyInput(c2));
        }

        private KeyInputSet CreateMany(params char[] all)
        {
            return KeyInputSet.NewManyKeyInputs(all.Select(InputUtil.CharToKeyInput).ToFSharpList());
        }

        [Test]
        public void Add1()
        {
            var name1 = KeyInputSet.NewOneKeyInput(InputUtil.CharToKeyInput('c'));
            var name2 = name1.Add(InputUtil.CharToKeyInput('a'));
            Assert.AreEqual("ca", name2.Name);
        }

        [Test]
        public void Name1()
        {
            var name1 = KeyInputSet.NewOneKeyInput(InputUtil.CharToKeyInput('c'));
            Assert.AreEqual("c", name1.Name);
        }

        [Test]
        public void Equality()
        {
            EqualityUtil.RunAll(
                (left, right) => left == right,
                (left, right) => left != right,
                false,
                true,
                EqualityUnit.Create(CreateOne('a')).WithEqualValues(CreateOne('a')),
                EqualityUnit.Create(CreateOne('a')).WithNotEqualValues(CreateOne('b')),
                EqualityUnit.Create(CreateOne('a')).WithEqualValues(CreateMany('a')),
                EqualityUnit.Create(CreateOne('D')).WithEqualValues(KeyNotationUtil.StringToKeyInputSet("D")),
                EqualityUnit.Create(KeyInputSet.NewOneKeyInput(InputUtil.CharToKeyInput('D'))).WithEqualValues(KeyNotationUtil.StringToKeyInputSet("D")));
        }
    }
}
