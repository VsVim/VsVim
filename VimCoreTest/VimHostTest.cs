using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using Vim.Extensions;

namespace Vim.UnitTest
{
    public sealed class VimHostTest : VimTestBase
    {
        /// <summary>
        /// Make sure that we respect the host policy on whether or not an IVimBuffer should be created for a given
        /// ITextView
        /// </summary>
        [Fact]
        public void RespectHostCreationPolicy()
        {
            VimHost.ShouldCreateVimBufferImpl = false;
            var textView = CreateTextView();
            IVimBuffer vimBuffer;
            Assert.False(Vim.TryGetVimBuffer(textView, out vimBuffer));
            Assert.Null(vimBuffer);
        }
    }
}
