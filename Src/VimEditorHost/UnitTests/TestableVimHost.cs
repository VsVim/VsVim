#if VS_UNIT_TEST_HOST
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    [Export(typeof(IVimHost))]
    public sealed class TestableVimHost : MockVimHost
    {
        [ImportingConstructor]
        public TestableVimHost()
        {
        }
    }
}
#endif
