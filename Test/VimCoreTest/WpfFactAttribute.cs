using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace Vim.UnitTest
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfFactDiscoverer", "Vim.Core.UnitTest")]
    public class WpfFactAttribute : FactAttribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfTheoryDiscoverer", "Vim.Core.UnitTest")]
    public class WpfTheoryAttribute : TheoryAttribute { }
}
