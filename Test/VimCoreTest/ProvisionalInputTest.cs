using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using EditorUtils;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test cases for provisional input (IME)
    /// </summary>
    public abstract class ProvisionalInputTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly ITextBuffer _textBuffer;

        protected ProvisionalInputTest()
        {
            _vimBuffer = CreateVimBuffer("cat chases the dog");
            _textBuffer = _vimBuffer.TextBuffer;
        }

        /// <summary>
        /// Disabled mode should behave like Vim is completely disabled.  Don't process any input including
        /// provisional one 
        /// </summary>
        public sealed class DisabledModeTest : ProvisionalInputTest
        {
            [Fact(Skip = "IME")]
            public void CanProcessProvisional()
            {
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                Assert.False(_vimBuffer.CanProcessProvisional('a'));
            }

            [Fact(Skip = "IME")]
            public void ProcessProvisional()
            {
                _textBuffer.SetText("cat");
                _vimBuffer.SwitchMode(ModeKind.Disabled, ModeArgument.None);
                _vimBuffer.ProcessProvisional('a');
                Assert.Equal("cat", _textBuffer.CurrentSnapshot.GetText());
            }
        }

        /// <summary>
        /// Insert mode should just be inserting and replacing the text as the normal editor would during
        /// typing 
        /// </summary>
        public sealed class InsertModeTest : ProvisionalInputTest
        {
            public InsertModeTest()
            {
                _textBuffer.SetText("");
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
            }

            [Fact(Skip = "IME")]
            public void Simple()
            {
                const string text = "cat";
                for (int i = 0; i < text.Length; i++) 
                {
                    _vimBuffer.ProcessProvisional(text[i]);
                    Assert.Equal(text[i].ToString(), _textBuffer.CurrentSnapshot.GetText());
                }

                _vimBuffer.Process("a");
                Assert.Equal("a", _textBuffer.CurrentSnapshot.GetText());
            }

            /// <summary>
            /// When the mode is exited then just commit the IME input as is.  Not a whole lot of options
            /// besides that.  
            /// </summary>
            [Fact(Skip = "IME")]
            public void LeaveMode()
            {
                _vimBuffer.ProcessProvisional('h');
                _vimBuffer.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _vimBuffer.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _vimBuffer.Process("i");
                Assert.Equal("hi", _textBuffer.CurrentSnapshot.GetText());
            }
        }
    }
}
