using Microsoft.FSharp.Collections;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class KeyInputSetTest
    {
        [Fact]
        public void Compare_AlternateKeyInputShouldBeEqual()
        {
            var left = KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey);
            var right = KeyInputSet.NewOneKeyInput(KeyInputUtil.CharWithControlToKeyInput('m'));
            Assert.True(0 == left.CompareTo(right));
            Assert.True(0 == right.CompareTo(left));
        }

        [Fact]
        public void Compare_AlternateKeyInputShouldBeEqualInMap()
        {
            var left = KeyInputSet.NewOneKeyInput(KeyInputUtil.EnterKey);
            var right = KeyInputSet.NewOneKeyInput(KeyInputUtil.CharWithControlToKeyInput('m'));
            var map = MapModule.Empty<KeyInputSet, bool>().Add(left, true);
            var result = MapModule.TryFind(right, map);
            Assert.True(result.IsSome());

            map = MapModule.Empty<KeyInputSet, bool>().Add(right, true);
            result = MapModule.TryFind(left, map);
            Assert.True(result.IsSome());
        }
    }
}
