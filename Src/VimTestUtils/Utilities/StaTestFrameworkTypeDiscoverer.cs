using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Vim.Extensions;
using System.Windows.Threading;
using System.Diagnostics;
using Xunit.Sdk;
using Xunit.Abstractions;

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
