using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class AutoCommandRunnerTest : VimTestBase
    {
        private readonly AutoCommandRunner _autoCommandRunner;

        protected AutoCommandRunnerTest()
        {
            _autoCommandRunner = new AutoCommandRunner(Vim);
        }

        public sealed class RunAutoCommandsTest : AutoCommandRunnerTest
        {
            /// <summary>
            /// When disabled don't run any commands
            /// </summary>
            [WpfFact]
            public void IgnoreWhenDisabled()
            {
                VimHost.IsAutoCommandEnabled = false;
                VimData.AddAutoCommand(EventKind.FileType, "xml", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.xml");
                _autoCommandRunner.RunAutoCommands(vimBuffer, EventKind.FileType);
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [WpfFact]
            public void WrongPattern()
            {
                VimData.AddAutoCommand(EventKind.FileType, "xml", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                _autoCommandRunner.RunAutoCommands(vimBuffer, EventKind.FileType);
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [WpfFact]
            public void Simple()
            {
                VimData.AddAutoCommand(EventKind.FileType, "xml", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.xml");
                _autoCommandRunner.RunAutoCommands(vimBuffer, EventKind.FileType);
                Assert.Equal(12, vimBuffer.LocalSettings.TabStop);
            }

            [WpfFact]
            public void SimpleWithAltPattern()
            {
                VimData.AddAutoCommand(EventKind.FileType, "*xml", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.xml");
                _autoCommandRunner.RunAutoCommands(vimBuffer, EventKind.FileType);
                Assert.Equal(12, vimBuffer.LocalSettings.TabStop);
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

            [WpfFact]
            public void SimpleWildCard()
            {
                AssertMatch("foo.html", "*.html");
                AssertMatch("foo.txt", "*.txt");
            }

            /// <summary>
            /// Matches implicitly must match the end of the string
            /// </summary>
            [WpfFact]
            public void NotAtEnd()
            {
                AssertNotMatch("foo.html", "*.ht");
                AssertNotMatch("txt.html", "*txt");
                AssertNotMatch("bar.txt.html", "*.txt");
            }

            [WpfFact]
            public void OrPattern()
            {
                AssertMatch("test.h", @"*.\(c\|cpp\|h\)");
                AssertMatch("test.cpp", @"*.\(c\|cpp\|h\)");
                AssertMatch("test.c", @"*.\(c\|cpp\|h\)");

                AssertMatch("test.h", @"*.{c,cpp,h}");
                AssertMatch("test.cpp", @"*.{c,cpp,h}");
                AssertMatch("test.c", @"*.{c,cpp,h}");

                AssertMatch("test.h", @"*.c,*.cpp,*.h");
                AssertMatch("test.cpp", @"*.c,*.cpp,*.h");
                AssertMatch("test.c", @"*.c,*.cpp,*.h");
            }

            [Fact]
            public void Literals()
            {
                AssertMatch("a", @"*");
                AssertMatch("a", @"?");
                AssertMatch("?", @"\?");
                AssertMatch(".", @".");
                AssertMatch("~", @"~");
                AssertMatch(",", @"\,");
                AssertMatch("}", @"\}");
                AssertMatch("{", @"\{");
                AssertMatch("a", @"[az]");
                AssertMatch("b", @"[^az]");
                AssertMatch("aBc42", @"[a-zA-Z0-9]\\\{5,6\}");
            }

            [Fact]
            public void Simple()
            {
                AssertMatch("readme.txt", @"*.txt");
                AssertMatch("~/.vimrc", @"~/.vimrc");
                AssertMatch("/tmp/doc/xx.txt", @"*/doc/*.txt");
                AssertMatch("/usr/home/piet/doc/yy.txt", @"*/doc/*.txt");
                AssertMatch(@"c:\code\sampleProject\example1.c", @"*.[ch],*.hpp,*.cpp");
                AssertMatch("test.z", @"*.\{c,cpp,h\},*.z");

                AssertNotMatch(@"c:\code\sampleProject\example1.z", @"*.[ch],*.hpp,*.cpp");
            }
        }
    }
}
