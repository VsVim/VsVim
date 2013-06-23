using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VsVim.Implementation.Misc;
using Xunit;

namespace VsVim.Shared.UnitTest
{
    public abstract class ScopeDataTest
    {
        public sealed class ImportantScopeTest : ScopeDataTest
        {
            [Fact]
            public void IsImportantScope1()
            {
                var scopeData = ScopeData.Default;
                Assert.True(scopeData.IsImportantScope("Global"));
                Assert.True(scopeData.IsImportantScope("Text Editor"));
                Assert.True(scopeData.IsImportantScope(String.Empty));
            }

            [Fact]
            public void IsImportantScope2()
            {
                var scopeData = ScopeData.Default;
                Assert.False(scopeData.IsImportantScope("blah"));
                Assert.False(scopeData.IsImportantScope("VC Image Editor"));
            }
        }
    }
}
