using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.Extensions;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class AutoCommandRunnerTest : VimTestBase
    {
        protected void AddAutoCommand(EventKind eventKind, string pattern, string command)
        {
            var autoCommand = new AutoCommand(
                AutoCommandGroup.Default,
                (new[] { eventKind }).ToFSharpList(),
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
    }
}
