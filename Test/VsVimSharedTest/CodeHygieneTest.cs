using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// Pedantic code hygiene tests for the code base
    /// </summary>
    public sealed class CodeHygieneTest
    {
        private readonly Assembly _assembly = typeof(CodeHygieneTest).Assembly;

        [Fact]
        public void TestNamespace()
        {
            const string prefix = "Vim.VisualStudio.UnitTest.";
            foreach (var type in _assembly.GetTypes().Where(x => x.IsPublic))
            {
                Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal), String.Format("Wrong namespace prefix on {0}", type.FullName));
            }
        }

        [Fact]
        public void CodeNamespace()
        {
            const string prefix = "Vim.VisualStudio.";
            var assemblies = new[]
                {
                    typeof(ISharedService).Assembly,
                    typeof(IVsAdapter).Assembly
                };
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.FullName.StartsWith("Xaml", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal), String.Format("Wrong namespace prefix on {0}", type.FullName));
                }
            }
        }
    }
}
