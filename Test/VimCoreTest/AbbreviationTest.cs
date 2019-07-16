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
            private readonly LocalAbbreviationMap _map;

            public AbbreviationKindTest()
            {
                _textBuffer = CreateTextBuffer();
                _localSettings = new LocalSettings(Vim.GlobalSettings);
                _wordUtil = new WordUtil(_textBuffer, _localSettings);
                _map = new LocalAbbreviationMap(Vim.GlobalAbbreviationMap, _wordUtil);
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
                var kind = _map.TryParse(item);
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
                var kind = _map.TryParse(item);
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
                var kind = _map.TryParse(item);
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
                var kind = _map.TryParse(item);
                Assert.True(kind.IsNone());
            }
        }

        public sealed class AbbreviationDataTest : AbbreviationTest
        {
            private static AbbreviationMode ParseMode(string mode) =>
                mode == "i" ? AbbreviationMode.Insert : AbbreviationMode.Command;

            private static AbbreviationData ParseData(string data)
            {
                var parts = data.Split(' ');
                switch (parts[0])
                {
                    case "a":
                        return new AbbreviationData(
                            KeyNotationUtil.StringToKeyInputSet(parts[1]),
                            KeyNotationUtil.StringToKeyInputSet(parts[2]));
                    case "si":
                        return new AbbreviationData(
                            KeyNotationUtil.StringToKeyInputSet(parts[1]),
                            KeyNotationUtil.StringToKeyInputSet(parts[2]),
                            AbbreviationMode.Insert);
                    case "sc":
                        return new AbbreviationData(
                            KeyNotationUtil.StringToKeyInputSet(parts[1]),
                            KeyNotationUtil.StringToKeyInputSet(parts[2]),
                            AbbreviationMode.Command);
                    case "m":
                        return new AbbreviationData(
                            KeyNotationUtil.StringToKeyInputSet(parts[1]),
                            KeyNotationUtil.StringToKeyInputSet(parts[2]),
                            KeyNotationUtil.StringToKeyInputSet(parts[3]));
                    default:
                        throw new Exception();
                }
            }

            [WpfTheory]
            [InlineData("a dd dog", "i", "cat", "* dd - cat - dog")]
            [InlineData("m dd dog cat", "c", "dog", "! dd - dog")]
            [InlineData("sc dd dog", "i", "dog", "! dd - dog")]
            [InlineData("sc dd dog", "c", "dog", "c dd - dog")]
            [InlineData("si dd dog", "i", "dog", "i dd - dog")]
            [InlineData("si dd dog", "c", "cat", "* dd - dog - cat")]
            public void ChangeReplacement(string data, string mode, string newReplacement, string expected)
            {
                var abbreviationData = ParseData(data);
                var newData = abbreviationData.ChangeReplacement(ParseMode(mode), KeyNotationUtil.StringToKeyInputSet(newReplacement));
                Assert.Equal(expected, newData.ToString());
            }

            [WpfTheory]
            [InlineData("a dd dog", "i", "c dd - dog")]
            [InlineData("si dd dog", "i", null)]
            [InlineData("sc dd dog", "i", "c dd - dog")]
            [InlineData("m dd dog cat", "c", "i dd - dog")]
            public void RemoveReplacement(string data, string mode, string expected)
            {
                var abbreviationData = ParseData(data);
                var newData = abbreviationData.RemoveReplacement(ParseMode(mode));
                if (expected is null)
                {
                    Assert.True(newData.IsNone());
                }
                else
                {
                    Assert.Equal(expected, newData.Value.ToString());
                }
            }
        }
    }
}
