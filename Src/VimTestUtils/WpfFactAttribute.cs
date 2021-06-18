using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vim.VisualStudio.Specific;
using Xunit;
using Xunit.Extensions;
using Xunit.Sdk;

namespace Vim.UnitTest
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfFactDiscoverer", "Vim.UnitTest.Utils")]
    public class WpfFactAttribute : FactAttribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfTheoryDiscoverer", "Vim.UnitTest.Utils")]
    public class WpfTheoryAttribute : TheoryAttribute { }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfFactDiscoverer", "Vim.UnitTest.Utils")]
    public class LegacyCompletionWpfFactAttribute : WpfFactAttribute
    {
        public override string Skip => VimSpecificUtil.HasLegacyCompletion
            ? null
            : $"Test only supported on legacy completion";

        public LegacyCompletionWpfFactAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfFactDiscoverer", "Vim.UnitTest.Utils")]
    public class Vs2017AndAboveWpfFactAttribute : WpfFactAttribute
    {
        public override string Skip =>
#if VS_SPECIFIC_2015
            $"Test only supported in VS 2017 and above";
#else
            null;
#endif

        public Vs2017AndAboveWpfFactAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [XunitTestCaseDiscoverer("Vim.UnitTest.Utilities.WpfFactDiscoverer", "Vim.UnitTest.Utils")]
    public class AsyncCompletionWpfFactAttribute : WpfFactAttribute
    {
        public override string Skip => VimSpecificUtil.HasAsyncCompletion
            ? null
            : $"Test only supported on async completion";

        public AsyncCompletionWpfFactAttribute() { }
    }
}
