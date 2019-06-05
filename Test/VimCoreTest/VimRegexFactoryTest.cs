using Xunit;
using Vim.Extensions;
using System.Text.RegularExpressions;

namespace Vim.UnitTest
{
    public abstract class VimRegexFactoryTest
    {
        protected IVimGlobalSettings _globalSettings;

        protected VimRegexFactoryTest()
        {
            _globalSettings = new GlobalSettings();
        }

        public sealed class CreateForSubstituteFlagsTest : VimRegexFactoryTest
        {
            private VimRegex Create(string pattern, SubstituteFlags flags = SubstituteFlags.None)
            {
                var regex = VimRegexFactory.CreateForSubstituteFlags(pattern, _globalSettings, flags);
                Assert.True(regex.IsSome());
                return regex.Value;
            }

            /// <summary>
            /// If there is no explicit case option in the flags then the ignore case setting
            /// should be respected
            /// </summary>
            [Fact]
            public void RespectIgnoreCaseSetting()
            {
                _globalSettings.IgnoreCase = true;
                var regex = Create("test", SubstituteFlags.None);
                Assert.True(regex.Regex.Options.HasFlag(RegexOptions.IgnoreCase));
            }

            /// <summary>
            /// The flag should trump the setting
            /// </summary>
            [Fact]
            public void RespectIgnoreCaseFlag()
            {
                _globalSettings.IgnoreCase = false;
                var regex = Create("test", SubstituteFlags.IgnoreCase);
                Assert.True(regex.Regex.Options.HasFlag(RegexOptions.IgnoreCase));
            }

            /// <summary>
            /// Smart case should be considered if there is no explicit case flag
            /// </summary>
            [Fact]
            public void RespectSmartCaseSetting()
            {
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                var regex = Create("Test", SubstituteFlags.None);
                Assert.False(regex.Regex.Options.HasFlag(RegexOptions.IgnoreCase));
            }

            /// <summary>
            /// The ignore flag should override smart case
            /// </summary>
            [Fact]
            public void IgnoreSmartCaseWithIgnoreCaseFlag()
            {
                _globalSettings.IgnoreCase = true;
                _globalSettings.SmartCase = true;
                var regex = Create("Test", SubstituteFlags.IgnoreCase);
                Assert.True(regex.Regex.Options.HasFlag(RegexOptions.IgnoreCase));
            }

            [Fact]
            public void Collection()
            {
                var regex = Create(@"[a]");
                Assert.Equal(@"[a]", regex.RegexPattern);
            }

            [Fact]
            public void AlternateCollection()
            {
                var regex = Create(@"\_[a]");
                Assert.Equal(@"[\r\na]", regex.RegexPattern);
            }

            [Fact]
            public void InvertedCollection()
            {
                var regex = Create(@"[^a]");
                Assert.Equal(@"[^\r\na]", regex.RegexPattern);
            }

            [Fact]
            public void AlternateInvertedCollection()
            {
                var regex = Create(@"\_[^a]");
                Assert.Equal(@"[^a]", regex.RegexPattern);
            }
        }
    }
}
