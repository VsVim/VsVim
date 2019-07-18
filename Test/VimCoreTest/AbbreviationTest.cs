using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Vim.Extensions;
using Microsoft.VisualStudio.Text;

namespace Vim.UnitTest
{
    public abstract class AbbreviationTest : VimTestBase
    {
        public sealed class AbbreviationKindTest : AbbreviationTest
        {
            private readonly ITextBuffer _textBuffer;
            private readonly WordUtil _wordUtil;
            private readonly IVimLocalSettings _localSettings;
            private readonly IVimLocalKeyMap _localKeyMap;
            private readonly LocalAbbreviationMap _map;

            public AbbreviationKindTest()
            {
                _textBuffer = CreateTextBuffer();
                _localSettings = new LocalSettings(Vim.GlobalSettings);
                _wordUtil = new WordUtil(_textBuffer, _localSettings);
                _localKeyMap = new LocalKeyMap(Vim.GlobalKeyMap, Vim.GlobalSettings, Vim.VariableMap);
                _map = new LocalAbbreviationMap(_localKeyMap, Vim.GlobalAbbreviationMap, _wordUtil);
            }

            [WpfTheory]
            [InlineData("foo", null)]
            [InlineData("g3", null)]
            [InlineData("-a", "@,-")]
            public void FullId(string item, string isKeyword)
            {
                if (isKeyword is object)
                {
                    _localSettings.IsKeyword = isKeyword;
                }
                var kind = _map.Parse(item);
                Assert.True(kind.IsSome(x => x.IsFullId));
            }

            [WpfTheory]
            [InlineData("#i", null)]
            [InlineData("..f", null)]
            [InlineData("$/7", null)]
            public void EndId(string item, string isKeyword)
            {
                if (isKeyword is object)
                {
                    _localSettings.IsKeyword = isKeyword;
                }
                var kind = _map.Parse(item);
                Assert.True(kind.IsSome(x => x.IsEndId));
            }

            [WpfTheory]
            [InlineData("def#", null)]
            [InlineData("4/7$", null)]
            public void NonId(string item, string isKeyword)
            {
                if (isKeyword is object)
                {
                    _localSettings.IsKeyword = isKeyword;
                }
                var kind = _map.Parse(item);
                Assert.True(kind.IsSome(x => x.IsNonId));
            }

            [WpfTheory]
            [InlineData("a.b", null)]
            [InlineData("#def", null)]
            [InlineData("a b", null)]
            [InlineData("_$r", null)]
            public void Invalid(string item, string isKeyword)
            {
                if (isKeyword is object)
                {
                    _localSettings.IsKeyword = isKeyword;
                }
                var kind = _map.Parse(item);
                Assert.True(kind.IsNone());
            }
        }
    }
}
