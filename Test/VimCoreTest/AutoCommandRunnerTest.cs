using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class AutoCommandRunnerTest : VimTestBase
    {
        public sealed class FilePatternTest : AutoCommandRunnerTest
        {
            private static void AssertMatch(string fileName, string pattern)
            {
                Assert.True(AutoCommandRunner.FileNameEndsWithPattern(fileName, pattern));
            }

            private static void AssertNotMatch(string fileName, string pattern)
            {
                Assert.False(AutoCommandRunner.FileNameEndsWithPattern(fileName, pattern));
            }

            [Fact]
            public void SimpleWildCard()
            {
                AssertMatch("foo.html", "*.html");
                AssertMatch("foo.txt", "*.txt");
            }

            /// <summary>
            /// Matches implicitly must match the end of the string
            /// </summary>
            [Fact]
            public void NotAtEnd()
            {
                AssertNotMatch("foo.html", "*.ht");
                AssertNotMatch("txt.html", "*txt");
                AssertNotMatch("bar.txt.html", "*.txt");
            }

            [Fact]
            public void OrPattern()
            {
                AssertMatch("test.h", @"*.\(c\|cpp\|h\)");
                AssertMatch("test.cpp", @"*.\(c\|cpp\|h\)");
                AssertMatch("test.c", @"*.\(c\|cpp\|h\)");
            }
        }
    }
}
