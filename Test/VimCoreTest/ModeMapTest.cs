using Moq;
using Xunit;
using Vim.Extensions;
using System;

namespace Vim.UnitTest
{
    public sealed class ModeMapTest : VimTestBase
    {
        private readonly IVimBuffer _vimBuffer;
        private readonly ModeMap _modeMap;

        public ModeMapTest()
        {
            _vimBuffer = CreateVimBuffer();
            _modeMap = ((VimBuffer)_vimBuffer).ModeMap;
            _modeMap.Reset((new UninitializedMode(_vimBuffer.VimTextBuffer)));
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
            Assert.Equal(ModeKind.Normal, _modeMap.PreviousMode.Value.ModeKind);
        }

        /// <summary>
        /// When switching between visual modes the previous mode should remain unchanged.  It 
        /// doesn't actually reset the previous mode value.  Essentially when going from 
        /// insert -> visual character -> visual block, escape should go back to insert, not
        /// visual character. 
        /// </summary>
        [Fact]
        public void SwitchBetweenVisualModes()
        {
            foreach (var baseMode in new[] { ModeKind.Normal, ModeKind.Command, ModeKind.Insert })
            {
                _modeMap.SwitchMode(baseMode, ModeArgument.None);
                foreach (var visualMode in VisualKind.All)
                {
                    _modeMap.SwitchMode(visualMode.VisualModeKind, ModeArgument.None);
                }

                Assert.Equal(baseMode, _modeMap.PreviousMode.Value.ModeKind);
            }
        }

        [Fact]
        public void NormalToInsertToNormal()
        {
            _modeMap.SwitchMode(ModeKind.Insert, ModeArgument.None);
            _modeMap.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.Equal(ModeKind.Insert, _modeMap.PreviousMode.Value.ModeKind);
        }

        [Fact]
        public void InitialNormalHasNoPrevious()
        {
            Assert.Equal(ModeKind.Uninitialized, _modeMap.Mode.ModeKind);
            _modeMap.SwitchMode(ModeKind.Normal, ModeArgument.None);
            Assert.True(_modeMap.PreviousMode.IsNone());
        }

        /// <summary>
        /// Visual mode always needs a mode to fall back.  Make sure there is one available 
        /// if it's the very first mode and hence would otherwise have an uninitialized mode
        /// </summary>
        [Fact]
        public void InitialVisualHasNormalModeBackup()
        {
            foreach (var visualMode in VisualKind.All)
            {
                _modeMap.Reset((new UninitializedMode(_vimBuffer.VimTextBuffer)));
                _modeMap.SwitchMode(visualMode.VisualModeKind, ModeArgument.None);
                Assert.Equal(ModeKind.Normal, _modeMap.PreviousMode.value.ModeKind);
            }
        }

        [Fact]
        public void RecursiveSwitch()
        {
            var mode = new Mock<IMode>(MockBehavior.Loose);
            mode
                .Setup(x => x.OnEnter(ModeArgument.None))
                .Callback(() => _modeMap.SwitchMode(ModeKind.Command, ModeArgument.None));
            mode.SetupGet(x => x.ModeKind).Returns(ModeKind.VisualCharacter);
            _modeMap.RemoveMode(_vimBuffer.VisualCharacterMode);
            _modeMap.AddMode(mode.Object);
            Assert.Throws<InvalidOperationException>(() => _modeMap.SwitchMode(ModeKind.VisualCharacter, ModeArgument.None));
        }

        /// <summary>
        /// The mode switch is not complete until the event listeners have processed the change.
        /// </summary>
        [Fact]
        public void IsSwitchingModeInEvent()
        {
            _vimBuffer.SwitchedMode += (o, e) =>
            {
                Assert.True(_modeMap.IsSwitchingMode);
                Assert.True(_vimBuffer.IsSwitchingMode);
            };
            _modeMap.SwitchMode(ModeKind.Command, ModeArgument.None);
        }
    }
}

