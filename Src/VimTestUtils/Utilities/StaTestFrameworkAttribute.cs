using Xunit.Sdk;
using Xunit.Abstractions;
using System.Windows.Data;
using System;
using System.Linq;

namespace Vim.UnitTest.Utilities
{
    [AttributeUsage(AttributeTargets.Assembly)]
    [TestFrameworkDiscoverer("Vim.UnitTest.Utilities.StaTestFrameworkTypeDiscoverer", "Vim.UnitTest.Utils")]
    public sealed class StaTestFrameworkAttribute : Attribute, ITestFrameworkAttribute
    {
        public StaTestFrameworkAttribute()
        {
        }
    }
}
