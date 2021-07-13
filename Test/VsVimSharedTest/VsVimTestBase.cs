using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Vim.EditorHost;
using Vim.UnitTest;

namespace Vim.VisualStudio.UnitTest
{
    public class VsVimTestBase : VimTestBase
    {
        internal new VsVimHost VimHost => (VsVimHost)Vim.VimHost;

        protected override Type GetVimHostExportType() => typeof(VsVimHost);
    }
}
