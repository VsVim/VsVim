using Microsoft.FSharp.Collections;
using NUnit.Framework;
using Vim;
using Vim.Extensions;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class KeyInputSetTest
    {
        [Test]
        public void Compare_AlternateKeyInputShouldBeEqual()
        {
            var left = KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey);
            var right = KeyInputSet.NewOneKeyInput(KeyInputUtil.AlternateEnterKey);
            Assert.IsTrue(0 == left.CompareTo(right));
            Assert.IsTrue(0 == right.CompareTo(left));
        }

        [Test]
        public void Compare_AlternateKeyInputShouldBeEqualInMap()
        {
            var left = KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey);
            var right = KeyInputSet.NewOneKeyInput(KeyInputUtil.AlternateEnterKey);
            var map = MapModule.Empty<KeyInputSet, bool>().Add(left, true);
            var result = MapModule.TryFind(right, map);
            Assert.IsTrue(result.IsSome());

            map = MapModule.Empty<KeyInputSet, bool>().Add(right, true);
            result = MapModule.TryFind(left, map);
            Assert.IsTrue(result.IsSome());
        }
    }
}
