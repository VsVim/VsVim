using System.Linq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public class CommandNameTest
    {
        private KeyInputSet CreateOne(char c)
        {
            return new KeyInputSet(KeyInputUtil.CharToKeyInput(c));
        }

        private KeyInputSet CreateTwo(char c1, char c2)
        {
            var keyInputSet = new KeyInputSet(KeyInputUtil.CharToKeyInput(c1));
            return keyInputSet.Add(KeyInputUtil.CharToKeyInput(c2));
        }

        private KeyInputSet CreateMany(params char[] all)
        {
            return new KeyInputSet(all.Select(KeyInputUtil.CharToKeyInput).ToFSharpList());
        }

        [Fact]
        public void Add1()
        {
            var name1 = new KeyInputSet(KeyInputUtil.CharToKeyInput('c'));
            var name2 = name1.Add(KeyInputUtil.CharToKeyInput('a'));
            Assert.Equal("ca", name2.Name);
        }

        [Fact]
        public void Name1()
        {
            var name1 = new KeyInputSet(KeyInputUtil.CharToKeyInput('c'));
            Assert.Equal("c", name1.Name);
        }

        [Fact]
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
                EqualityUnit.Create(new KeyInputSet(KeyInputUtil.CharToKeyInput('D'))).WithEqualValues(KeyNotationUtil.StringToKeyInputSet("D")));
        }
    }
}
