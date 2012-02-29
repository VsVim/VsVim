using System.Linq;
using NUnit.Framework;
using Vim.Extensions;

namespace Vim.UnitTest
{
    [TestFixture]
    public class RegisterNameTest
    {
        [Test]
        public void AllChars1()
        {
            foreach (var cur in RegisterNameUtil.RegisterNameChars)
            {
                var res = RegisterName.OfChar(cur);
                Assert.IsTrue(res.IsSome());
            }
        }

        [Test]
        public void AllChars2()
        {
            var all = TestConstants.UpperCaseLetters
                + TestConstants.LowerCaseLetters
                + TestConstants.Digits
                + "~-_*+%:#";
            foreach (var cur in all)
            {
                Assert.IsTrue(RegisterNameUtil.RegisterNameChars.Contains(cur));
            }
        }

        [Test]
        public void AllChars3()
        {
            foreach (var cur in RegisterNameUtil.RegisterNameChars)
            {
                Assert.IsTrue(RegisterNameUtil.CharToRegister(cur).IsSome());
            }
        }

        [Test]
        public void All1()
        {
            Assert.AreEqual(74, RegisterName.All.Count());
        }

        /// <summary>
        /// It's the default if unnamed but does have the exlicit name "
        /// </summary>
        [Test]
        public void Unnamed()
        {
            Assert.AreEqual('"', RegisterName.Unnamed.Char.Value);
            Assert.AreEqual(RegisterName.Unnamed, RegisterNameUtil.CharToRegister('"').Value);
        }
    }
}
