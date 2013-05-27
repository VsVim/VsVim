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
            return vimBuffer;
        }

        public sealed class BufEnterTest : AutoCommandRunnerIntegrationTest
        {
            [Fact]
            public void SimpleMatching()
            {
                AddAutoCommand(EventKind.BufEnter, "*.html", "set ts=14");
                var vimBuffer = Create("foo.html");
                Assert.Equal(14, vimBuffer.LocalSettings.TabStop);
            }
        }

        public sealed class EnabledTest : AutoCommandRunnerIntegrationTest
        {
            [Fact]
            public void NoEventsOnDisable()
            {
                AddAutoCommand(EventKind.BufEnter, "*.html", "set ts=12");
                _globalSettings.IsAutoCommandEnabled = false;
                var vimBuffer = Create("foo.html");
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void NoEventsOnDisableClose()
            {
                _globalSettings.IsAutoCommandEnabled = false;
                AddAutoCommand(EventKind.BufWinLeave, "*.html", "set ts=12");
                var vimBuffer = Create("foo.html");
                vimBuffer.Close();
                Assert.Equal(8, vimBuffer.LocalSettings.TabStop);
            }

            [Fact]
            public void DisableChange()
            {
                _globalSettings.IsAutoCommandEnabled = false;
                AddAutoCommand(EventKind.BufWinLeave, "*.html", "set ts=12");
                var vimBuffer = Create("foo.html");
                _globalSettings.IsAutoCommandEnabled = true;
                vimBuffer.Close();
                Assert.Equal(12, vimBuffer.LocalSettings.TabStop);
            }
        }
    }
}
