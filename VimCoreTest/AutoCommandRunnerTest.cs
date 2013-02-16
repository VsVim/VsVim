using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class AutoCommandRunnerTest : VimTestBase
    {
        protected void AddAutoCommand(EventKind eventKind, string pattern, string command)
        {
            var autoCommand = new AutoCommand(
                AutoCommandGroup.Default,
                eventKind,
                command,
                pattern);
            VimData.AutoCommands = VimData.AutoCommands.Concat(new[] { autoCommand }).ToFSharpList();
        }

        private IVimBuffer Create(string fileName, params string[] lines)
        {
            VimHost.FileName = fileName;
            var vimBuffer = CreateVimBuffer(lines);
            VimHost.FileName = "";
            return vimBuffer;
        }

        public sealed class BufEnterTest : AutoCommandRunnerTest
        {
            [Fact]
            public void SimpleMatching()
            {
                AddAutoCommand(EventKind.BufEnter, "*.html", "set ts=14");
                var vimBuffer = Create("foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }
        }

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
