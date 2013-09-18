using Xunit;

namespace Vim.UnitTest
{
    public abstract class ModeMapTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly ModeMap _modeMap;

        protected ModeMapTest()
        {
            _vimBuffer = CreateVimBuffer();
            _modeMap = ((VimBuffer)_vimBuffer).ModeMap;
        }

        public sealed class PreviousModeTest : ModeMapTest
        {
            public PreviousModeTest()
            {
                Assert.Equal(ModeKind.Normal, _modeMap.Mode.ModeKind);
            }

            /// <summary>
            /// Make sure that "normal" mode is properly logged as the previous mode when 
            /// transitioning between normal -> insert -> visual 
            /// </summary>
            [Fact]
            public void InsertToNormalToVisualCharacter()
            {
                _modeMap.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _modeMap.SwitchMode(ModeKind.Normal, ModeArgument.None);
                _modeMap.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                Assert.Equal(ModeKind.Normal, _modeMap.PreviousMode.ModeKind);
            }

            /// <summary>
            /// When switching between visual modes we don't want to change the previous mode to a 
            /// non-visual mode.  Instead keep the previous non-visual mode 
            /// </summary>
            [Fact]
            public void VisualCharacterToVisualBlock()
            {
                _modeMap.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None);
                _modeMap.SwitchMode(ModeKind.VisualLine, ModeArgument.None);
                Assert.Equal(ModeKind.Normal, _modeMap.PreviousMode.ModeKind);
            }

            [Fact]
            public void NormalToInsertToNormal()
            {
                _modeMap.SwitchMode(ModeKind.Insert, ModeArgument.None);
                _modeMap.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.Equal(ModeKind.Insert, _modeMap.PreviousMode.ModeKind);
            }
        }

        public sealed class MiscTest : ModeMapTest
        {
            /// <summary>
            /// Make sure that we properly transition to normal mode when leaving visual mode 
            /// </summary>
            [Fact]
            public void Issue1170()
            {
                _vimBuffer.ProcessNotation(@"i<Esc>v<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer.ModeKind);
            }
        }
    }
}

