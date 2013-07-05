using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Xunit;

namespace Vim.UI.Wpf.UnitTest
{
    /// <summary>
    /// Pedantic code hygiene tests for the code base
    /// </summary>
    public sealed class CodeHygieneTest
    {
        private readonly Assembly _assembly = typeof(CodeHygieneTest).Assembly;

        [Fact]
        public void Namespace()
        {
            const string prefix = "Vim.UI.Wpf.UnitTest.";
            foreach (var type in _assembly.GetTypes().Where(x => x.IsPublic))
            {
                Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal));
            }
        }
    }
}
