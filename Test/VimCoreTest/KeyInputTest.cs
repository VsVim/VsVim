using System;
using System.Linq;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public class KeyInputTest
    {
        [Fact]
        public void IsDigit1()
        {
            var input = KeyInputUtil.CharToKeyInput('0');
            Assert.True(input.IsDigit);
        }

        [Fact]
        public void IsDigit2()
        {
            var input = KeyInputUtil.EnterKey;
            Assert.False(input.IsDigit);
        }

        [Fact]
        public void IsFunction_Not()
        {
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(cur);
                Assert.False(keyInput.IsFunctionKey);
            }
        }

        [Fact]
        public void IsFunction_All()
        {
            foreach (var number in Enumerable.Range(1, 12))
            {
                var name = "F" + number;
                var vimKey = (VimKey)(Enum.Parse(typeof(VimKey), name));
                var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
                Assert.True(keyInput.IsFunctionKey);
            }
        }

        /// <summary>
        /// The key pad should register as digits.  Otherwise it won't be included as count
        /// values
        /// </summary>
        [Fact]
        public void IsDigit_KeyPad()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>().Where(VimKeyUtil.IsKeypadNumberKey))
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.True(keyInput.IsDigit);
                Assert.True(keyInput.RawChar.IsSome());
                Assert.True(CharUtil.IsDigit(keyInput.Char));
            }
        }

        [Fact]
        public void Equality1()
        {
            var i1 = VimUtil.CreateKeyInput(c: 'c');
            Assert.Equal(i1, VimUtil.CreateKeyInput(c: 'c'));
            Assert.NotEqual(i1, VimUtil.CreateKeyInput(c: 'd'));
            Assert.NotEqual(i1, VimUtil.CreateKeyInput(c: 'c', mod: KeyModifiers.Shift));
            Assert.NotEqual(i1, VimUtil.CreateKeyInput(c: 'c', mod: KeyModifiers.Alt));
        }

        /// <summary>
        /// Boundary condition
        /// </summary>
        [Fact]
        public void Equality2()
        {
            var i1 = VimUtil.CreateKeyInput(c: 'c');
            Assert.NotEqual<object>(i1, 42);
        }

        [Fact]
        public void Equality3()
        {
            Assert.True(KeyInputUtil.CharToKeyInput('a') == KeyInputUtil.CharToKeyInput('a'));
            Assert.True(KeyInputUtil.CharToKeyInput('b') == KeyInputUtil.CharToKeyInput('b'));
            Assert.True(KeyInputUtil.CharToKeyInput('c') == KeyInputUtil.CharToKeyInput('c'));
        }

        [Fact]
        public void Equality4()
        {
            Assert.True(KeyInputUtil.CharToKeyInput('a') != KeyInputUtil.CharToKeyInput('b'));
            Assert.True(KeyInputUtil.CharToKeyInput('b') != KeyInputUtil.CharToKeyInput('c'));
            Assert.True(KeyInputUtil.CharToKeyInput('c') != KeyInputUtil.CharToKeyInput('d'));
        }

        [Fact]
        public void Equality5()
        {
            var values = EqualityUnit
                 .Create(KeyInputUtil.CharToKeyInput('c'))
                 .WithEqualValues(KeyInputUtil.CharToKeyInput('c'))
                 .WithNotEqualValues(KeyInputUtil.CharToKeyInput('d'))
                 .WithNotEqualValues(KeyInputUtil.CharWithControlToKeyInput('c'));
            EqualityUtil.RunAll(
                (x, y) => x == y,
                (x, y) => x != y,
                values: values);
        }

        [Fact]
        public void Equality_ControlLetterIsCaseInsensitive()
        {
            var unit = EqualityUnit.Create(KeyInputUtil.CharWithControlToKeyInput('a'))
                .WithEqualValues(KeyInputUtil.CharWithControlToKeyInput('A'))
                .WithNotEqualValues(KeyInputUtil.CharToKeyInput('a'));
            EqualityUtil.RunAll(
                (x, y) => x == y,
                (x, y) => x != y,
                values: unit);
        }

        [Fact]
        public void CompareTo1()
        {
            var i1 = KeyInputUtil.CharToKeyInput('c');
            Assert.True(i1.CompareTo(KeyInputUtil.CharToKeyInput('z')) < 0);
            Assert.True(i1.CompareTo(KeyInputUtil.CharToKeyInput('c')) == 0);
            Assert.True(i1.CompareTo(KeyInputUtil.CharToKeyInput('a')) > 0);
        }

        [Fact]
        public void CompareSemantics()
        {
            var allKeyInputs = KeyInputUtil.VimKeyInputList;
            var all = allKeyInputs.SelectMany(x => new[] {
                x,
                KeyInputUtil.ChangeKeyModifiersDangerous(x, KeyModifiers.Control),
                KeyInputUtil.ChangeKeyModifiersDangerous(x, KeyModifiers.Shift),
                KeyInputUtil.ChangeKeyModifiersDangerous(x, KeyModifiers.Alt)
            });

            foreach (var left in all)
            {
                foreach (var right in all)
                {
                    var result1 = left.CompareTo(right);
                    var result2 = right.CompareTo(left);
                    if (result1 == result2)
                    {
                        Assert.Equal(0, result1);
                        Assert.Equal(left.GetHashCode(), right.GetHashCode());
                    }
                    else if (result1 < 0)
                    {
                        Assert.True(result2 > 0);
                    }
                    else if (result2 < 0)
                    {
                        Assert.True(result1 > 0);
                    }
                    else
                    {
                        throw new Exception("failed");
                    }
                }
            }
        }

        [Fact]
        public void GetHashCode_ControlLetterIsCaseInsensitive()
        {
            Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('a').GetHashCode(), KeyInputUtil.CharWithControlToKeyInput('A').GetHashCode());
        }

        [Fact]
        public void GetHashCode_ControlLetterIsCaseInsensitive2()
        {
            Assert.Equal(KeyInputUtil.CharWithControlToKeyInput('T').GetHashCode(), KeyInputUtil.CharWithControlToKeyInput('t').GetHashCode());
        }

    }
}
