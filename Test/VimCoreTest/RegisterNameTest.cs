using System.Linq;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public class RegisterNameTest
    {
        [Fact]
        public void AllChars1()
        {
            foreach (var cur in RegisterNameUtil.RegisterNameChars)
            {
                var res = RegisterName.OfChar(cur);
                Assert.True(res.IsSome());
            }
        }

        [Fact]
        public void AllChars2()
        {
            var all = TestConstants.UpperCaseLetters
                + TestConstants.LowerCaseLetters
                + TestConstants.Digits
                + "~-_*+%:#";
            foreach (var cur in all)
            {
                Assert.True(RegisterNameUtil.RegisterNameChars.Contains(cur));
            }
        }

        [Fact]
        public void AllChars3()
        {
            foreach (var cur in RegisterNameUtil.RegisterNameChars)
            {
                Assert.True(RegisterNameUtil.CharToRegister(cur).IsSome());
            }
        }

        [Fact]
        public void All1()
        {
            Assert.Equal(74, RegisterName.All.Count());
        }

        /// <summary>
        /// It's the default if unnamed but does have the exlicit name "
        /// </summary>
        [Fact]
        public void Unnamed()
        {
            Assert.Equal('"', RegisterName.Unnamed.Char.Value);
            Assert.Equal(RegisterName.Unnamed, RegisterNameUtil.CharToRegister('"').Value);
        }
    }
}
