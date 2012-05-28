using System;
using Xunit;

namespace Vim.UnitTest
{
    public sealed class VimDataTest : VimTestBase
    {
        /// <summary>
        /// The startup value for CurrentDirectory should be a non-empty string
        /// </summary>
        [Fact]
        public void CurrentDirectory_Initial()
        {
            IVimData vimData = new VimData();
            Assert.False(String.IsNullOrEmpty(vimData.CurrentDirectory));
        }

        /// <summary>
        /// Setting the current directory should move the previous value to PreviousCurrentDirectory
        /// </summary>
        [Fact]
        public void CurrentDirectory_SetUpdatePrevious()
        {
            IVimData vimData = new VimData();
            var old = vimData.CurrentDirectory;
            vimData.CurrentDirectory = @"c:\";
            Assert.Equal(old, vimData.PreviousCurrentDirectory);
        }

        /// <summary>
        /// The repeat last search should cause the search ran event to be raised
        /// </summary>
        [Fact]
        public void SearchRan_RepeatLastSearch()
        {
            var didRun = false;
            VimData.SearchRan += delegate { didRun = true; };
            VimData.LastPatternData = new PatternData("cat", Path.Forward);
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process("n");
            Assert.True(didRun);
        }

        /// <summary>
        /// The next word under cursor command '*' should cause the SearhRan event to fire
        /// </summary>
        [Fact]
        public void SearchRan_NextWordUnderCaret()
        {
            var didRun = false;
            VimData.SearchRan += delegate { didRun = true; };
            VimData.LastPatternData = new PatternData("cat", Path.Forward);
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process("*");
            Assert.True(didRun);
        }

        /// <summary>
        /// The '/' command should register a search change after the Enter key
        /// </summary>
        [Fact]
        public void SearchRan_IncrementalSerach()
        {
            var didRun = false;
            VimData.SearchRan += delegate { didRun = true; };
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process("/ab");
            Assert.False(didRun);
            vimBuffer.Process(VimKey.Enter);
            Assert.True(didRun);
        }

        /// <summary>
        /// The :s command should cause a SearchRan to occur
        /// </summary>
        [Fact]
        public void SearchRan_SubstituteCommand()
        {
            var didRun = false;
            VimData.SearchRan += delegate { didRun = true; };
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.ProcessNotation(":s/cat/bat<Enter>");
            Assert.True(didRun);
        }

        /// <summary>
        /// Don't raise the SearchRan command for simple commands
        /// </summary>
        [Fact]
        public void SearchRan_DontRaiseForSimpleCommands()
        {
            var didRun = false;
            VimData.SearchRan += delegate { didRun = true; };
            var vimBuffer = CreateVimBuffer("hello world");
            vimBuffer.Process("dd");
            Assert.False(didRun);
        }
    }
}
