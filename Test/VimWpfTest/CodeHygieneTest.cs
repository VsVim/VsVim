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

        // TODO_SHARED need to think about this in the new model
        // [Fact]
        private void Namespace()
        {
            const string prefix = "Vim.UI.Wpf.UnitTest.";
            foreach (var type in _assembly.GetTypes().Where(x => x.IsPublic))
            {
                Assert.StartsWith(prefix, type.FullName, StringComparison.Ordinal);
            }
        }
    }
}
