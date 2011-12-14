using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest.Exports
{
    [Export(typeof(IVimHost))]
    public sealed class VimHost : MockVimHost
    {
        [ImportingConstructor]
        public VimHost()
        {

        }
    }
}
