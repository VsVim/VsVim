using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim.VisualStudio.Implementation.Misc;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public abstract class ScopeDataTest
    {
        public sealed class ImportantScopeTest : ScopeDataTest
        {
            [Fact]
            public void GetScopeKind1()
            {
                var scopeData = ScopeData.Default;
                Assert.Equal(ScopeKind.Global, scopeData.GetScopeKind("Global"));
                Assert.Equal(ScopeKind.TextEditor, scopeData.GetScopeKind("Text Editor"));
                Assert.Equal(ScopeKind.EmptyName, scopeData.GetScopeKind(String.Empty));
            }

            [Fact]
            public void GetScopeKind2()
            {
                var scopeData = ScopeData.Default;
                Assert.Equal(ScopeKind.Unknown, scopeData.GetScopeKind("blah"));
                Assert.Equal(ScopeKind.Unknown, scopeData.GetScopeKind("VC Image Editor"));
            }
        }
    }
}
