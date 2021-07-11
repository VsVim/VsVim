using System;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Vim.UnitTest.Utilities
{
    public sealed class StaTestFrameworkTypeDiscoverer : ITestFrameworkTypeDiscoverer
    {
        public StaTestFrameworkTypeDiscoverer()
        {

        }

        Type ITestFrameworkTypeDiscoverer.GetTestFrameworkType(IAttributeInfo attribute) => typeof(StaTestFramework);
    }
}
