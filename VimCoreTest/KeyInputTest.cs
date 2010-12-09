using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.Test
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
            var input = KeyInputUtil.VimKeyToKeyInput(VimKey.Enter);
            Assert.IsFalse(input.IsDigit);
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
            Assert.AreEqual(KeyInputUtil.CharWithControlToKeyInput('a'), KeyInputUtil.CharWithControlToKeyInput('A'));
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
