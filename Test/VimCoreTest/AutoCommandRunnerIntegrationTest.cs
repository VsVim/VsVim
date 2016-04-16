using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.Extensions;
using Vim.Interpreter;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class AutoCommandRunnerIntegrationTest : VimTestBase
    {
        private readonly IVimGlobalSettings _globalSettings;

        protected AutoCommandRunnerIntegrationTest()
        {
            _globalSettings = Vim.GlobalSettings;
        }

        public sealed class BufEnterTest : AutoCommandRunnerIntegrationTest
        {
            [Fact]
            public void SimpleMatching()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*.html", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void NonMatchingPathIsNotApplied()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*/app/*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("/code/foo.html");
                Assert.NotEqual(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void PathSeparatorsCanBeUsedForUnixPaths()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, @"*\app\*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("/code/app/foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void AlternatePathSeparatorsCanBeUsedForUnixPaths()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*/app/*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("/code/app/foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void AlternatePathSeparatorsCanBeUsedForWindowsPaths()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*/app/*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName(@"c:\code\app\foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void PathSeparatorsCanBeUsedForWindowsPaths()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, @"*\app\*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName(@"c:\code\app\foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void CaseSensitive()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*/app/*", "set ts=14");
                var vimBuffer = CreateVimBufferWithName(@"c:\code\App\foo.html");
                Assert.NotEqual(14, vimBuffer.LocalSettings.TabStop);
            }
        }

        public sealed class FileTypeTest : AutoCommandRunnerIntegrationTest
        {
            [Fact]
            public void Simple()
            {
                VimData.AddAutoCommand(EventKind.FileType, "html", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }

            /// <summary>
            /// This event shouldn't fire on close.  Just on buffer creation
            /// </summary>
            [Fact]
            public void NotOnClose()
            {
                VimData.AddAutoCommand(EventKind.FileType, "html", "set ts=14");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                vimBuffer.LocalSettings.TabStop = 8;
                vimBuffer.Close();
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }
        }

        public sealed class EnabledTest : AutoCommandRunnerIntegrationTest
        {
            [Fact]
            public void NoEventsOnDisable()
            {
                VimData.AddAutoCommand(EventKind.BufEnter, "*.html", "set ts=12");
                VimHost.IsAutoCommandEnabled = false;
                var vimBuffer = CreateVimBufferWithName("foo.html");
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void NoEventsOnDisableClose()
            {
                VimHost.IsAutoCommandEnabled = false;
                VimData.AddAutoCommand(EventKind.BufWinLeave, "*.html", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                vimBuffer.Close();
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void DisableChange()
            {
                VimHost.IsAutoCommandEnabled = false;
                VimData.AddAutoCommand(EventKind.BufWinLeave, "*.html", "set ts=12");
                var vimBuffer = CreateVimBufferWithName("foo.html");
                VimHost.IsAutoCommandEnabled = true;
                vimBuffer.Close();
                Assert.Equal(12, vimBuffer.LocalSettings.TabStop);
            }
        }
    }
}
