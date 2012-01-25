using System;
using System.Linq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public class KeyInputTest
    {
        [Test]
        public void IsDigit1()
        {
            var input = KeyInputUtil.CharToKeyInput('0');
            Assert.IsTrue(input.IsDigit);
        }

        [Test]
        public void IsDigit2()
        {
            var input = KeyInputUtil.EnterKey;
            Assert.IsFalse(input.IsDigit);
        }

        [Test]
        public void IsFunction_Not()
        {
            foreach (var cur in KeyInputUtil.VimKeyCharList)
            {
                var keyInput = KeyInputUtil.CharToKeyInput(cur);
                Assert.IsFalse(keyInput.IsFunctionKey);
            }
        }

        [Test]
        public void IsFunction_All()
        {
            foreach (var number in Enumerable.Range(1, 12))
            {
                var name = "F" + number;
                var vimKey = (VimKey)(Enum.Parse(typeof(VimKey), name));
                var keyInput = KeyInputUtil.VimKeyToKeyInput(vimKey);
                Assert.IsTrue(keyInput.IsFunctionKey);
            }
        }

        /// <summary>
        /// The key pad should register as digits.  Otherwise it won't be included as count
        /// values
        /// </summary>
        [Test]
        public void IsDigit_KeyPad()
        {
            foreach (var cur in Enum.GetValues(typeof(VimKey)).Cast<VimKey>().Where(VimKeyUtil.IsKeypadNumberKey))
            {
                var keyInput = KeyInputUtil.VimKeyToKeyInput(cur);
                Assert.IsTrue(keyInput.IsDigit);
                Assert.IsTrue(keyInput.RawChar.IsSome());
                Assert.IsTrue(CharUtil.IsDigit(keyInput.Char));
            }
        }

        [Test]
        public void Equality1()
        {
            var i1 = VimUtil.CreateKeyInput(c: 'c');
            Assert.AreEqual(i1, VimUtil.CreateKeyInput(c: 'c'));
            Assert.AreNotEqual(i1, VimUtil.CreateKeyInput(c: 'd'));
            Assert.AreNotEqual(i1, VimUtil.CreateKeyInput(c: 'c', mod: KeyModifiers.Shift));
            Assert.AreNotEqual(i1, VimUtil.CreateKeyInput(c: 'c', mod: KeyModifiers.Alt));
        }

        [Test, Description("Boundary condition")]
        public void Equality2()
        {
            var i1 = VimUtil.CreateKeyInput(c: 'c');
            Assert.AreNotEqual(i1, 42);
        }

        [Test]
        public void Equality3()
        {
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('a') == KeyInputUtil.CharToKeyInput('a'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('b') == KeyInputUtil.CharToKeyInput('b'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('c') == KeyInputUtil.CharToKeyInput('c'));
        }

        [Test]
        public void Equality4()
        {
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('a') != KeyInputUtil.CharToKeyInput('b'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('b') != KeyInputUtil.CharToKeyInput('c'));
            Assert.IsTrue(KeyInputUtil.CharToKeyInput('c') != KeyInputUtil.CharToKeyInput('d'));
        }

        [Test]
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

        [Test]
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

        [Test]
        public void Equality_AlternatesNotEquivalentWhenModifierPresent()
        {
            Action<KeyInput, KeyInput> func = (left, right) =>
            {
                Assert.AreNotEqual(KeyInputUtil.ChangeKeyModifiers(left, KeyModifiers.Control), right);
                Assert.AreNotEqual(KeyInputUtil.ChangeKeyModifiers(left, KeyModifiers.Alt), right);
                Assert.AreNotEqual(KeyInputUtil.ChangeKeyModifiers(left, KeyModifiers.Shift), right);
            };

            foreach (var cur in KeyInputUtil.AlternateKeyInputPairList)
            {
                func(cur.Item1, cur.Item2);
            }
        }

        [Test]
        public void Equality_AlternatesAreEqual()
        {
            foreach (var pair in KeyInputUtil.AlternateKeyInputPairList)
            {
                var unit = EqualityUnit.Create(pair.Item1).WithEqualValues(pair.Item2).WithNotEqualValues(KeyInputUtil.VimKeyToKeyInput(VimKey.Colon));
                EqualityUtil.RunAll(
                    (x, y) => x == y,
                    (x, y) => x != y,
                    values: unit);
            }
        }

        [Test]
        public void CompareTo1()
        {
            var i1 = KeyInputUtil.CharToKeyInput('c');
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('z')) < 0);
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('c')) == 0);
            Assert.IsTrue(i1.CompareTo(KeyInputUtil.CharToKeyInput('a')) > 0);
        }

        [Test]
        public void CompareSemantics()
        {
            var allKeyInputs = Enumerable.Concat(KeyInputUtil.VimKeyInputList, KeyInputUtil.AlternateKeyInputList);
            var all = allKeyInputs.SelectMany(x => new[] {
                x,
                KeyInputUtil.ChangeKeyModifiers(x, KeyModifiers.Control),
                KeyInputUtil.ChangeKeyModifiers(x, KeyModifiers.Shift),
                KeyInputUtil.ChangeKeyModifiers(x, KeyModifiers.Alt)
            });

            foreach (var left in all)
            {
                foreach (var right in all)
                {
                    var altLeft = left.GetAlternate();
                    var altRight = right.GetAlternate();
                    var result1 = left.CompareTo(right);
                    var result2 = right.CompareTo(left);
                    if (result1 == result2)
                    {
                        Assert.AreEqual(0, result1);
                        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
                        if (altLeft.IsSome())
                        {
                            Assert.AreEqual(0, altLeft.Value.CompareTo(right));
                        }
                        if (altRight.IsSome())
                        {
                            Assert.AreEqual(0, left.CompareTo(altRight.Value));
                        }
                    }
                    else if (result1 < 0)
                    {
                        Assert.IsTrue(result2 > 0);
                        if (altLeft.IsSome())
                        {
                            Assert.IsTrue(altLeft.Value.CompareTo(right) < 0);
                            Assert.IsTrue(right.CompareTo(altLeft.Value) > 0);
                        }
                    }
                    else if (result2 < 0)
                    {
                        Assert.IsTrue(result1 > 0);
                        if (altLeft.IsSome())
                        {
                            Assert.IsTrue(altLeft.Value.CompareTo(right) > 0);
                            Assert.IsTrue(right.CompareTo(altLeft.Value) < 0);
                        }
                    }
                    else
                    {
                        Assert.Fail();
                    }
                }
            }
        }

        [Test]
        public void GetHashCode_ControlLetterIsCaseInsensitive()
        {
            Assert.AreEqual(KeyInputUtil.CharWithControlToKeyInput('a').GetHashCode(), KeyInputUtil.CharWithControlToKeyInput('A').GetHashCode());
        }

        [Test]
        public void GetHashCode_ControlLetterIsCaseInsensitive2()
        {
            Assert.AreEqual(KeyInputUtil.CharWithControlToKeyInput('T').GetHashCode(), KeyInputUtil.CharWithControlToKeyInput('t').GetHashCode());
        }

    }
}
