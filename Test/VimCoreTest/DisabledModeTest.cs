using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class DisabledModeTest : VimTestBase
    {
        public sealed class ProcessTest : DisabledModeTest
        {
            private readonly DisabledMode _modeRaw;
            private readonly IDisabledMode _mode;

            public ProcessTest()
            {
                var vimBufferData = CreateVimBufferData(CreateTextView(""));
                _modeRaw = new DisabledMode(vimBufferData);
                _mode = _modeRaw;
            }

            [Fact]
            public void CanProcess1()
            {
                Assert.True(_mode.CanProcess(GlobalSettings.DisableAllCommand));
            }

            [Fact]
            public void Commands1()
            {
                Assert.True(_mode.CommandNames.First().KeyInputs.First().Equals(GlobalSettings.DisableAllCommand));
            }

            [Fact]
            public void Process1()
            {
                var res = _mode.Process(GlobalSettings.DisableAllCommand);
                Assert.True(res.IsSwitchMode(ModeKind.Normal));
            }
        }

        public sealed class DisabledModeIntegrationTest : DisabledModeTest
        {
            private readonly IVimBuffer _vimBuffer1;
            private readonly IVimBuffer _vimBuffer2;

            public DisabledModeIntegrationTest()
            {
                _vimBuffer1 = CreateVimBuffer("vim buffer 1");
                _vimBuffer2 = CreateVimBuffer("vim buffer 2");
            }

            /// <summary>
            /// When one vim buffer is disabled then it should be disabling all IVimBuffer instances
            /// that are currently enabled
            /// </summary>
            [Fact]
            public void DisableAll()
            {
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.Disabled, _vimBuffer1.ModeKind);
                Assert.Equal(ModeKind.Disabled, _vimBuffer2.ModeKind);
                Assert.True(Vim.IsDisabled);
            }

            /// <summary>
            /// Setting the IsDisabled api to true should disable all of the active buffers exactly
            /// as if the command were processed
            /// </summary>
            [Fact]
            public void DisableViaApi()
            {
                Vim.IsDisabled = true;
                Assert.Equal(ModeKind.Disabled, _vimBuffer1.ModeKind);
                Assert.Equal(ModeKind.Disabled, _vimBuffer2.ModeKind);
                Assert.True(Vim.IsDisabled);
            }

            /// <summary>
            /// Disabled mode is a global state in addition to a local one.  Once it's turned on then all newly
            /// created IVimBuffer instances should also go into disabled mode
            /// </summary>
            [Fact]
            public void DisableNewBuffers()
            {
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                var newBuffer = CreateVimBuffer();
                Assert.Equal(ModeKind.Disabled, newBuffer.ModeKind);
            }

            /// <summary>
            /// Make sure that IVimTextBuffer instances have the correct mode as well
            /// </summary>
            [Fact]
            public void DisableNewTextBuffers()
            {
                Vim.IsDisabled = true;
                var newTextBuffer = CreateVimTextBuffer();
                Assert.Equal(ModeKind.Disabled, newTextBuffer.ModeKind);
            }

            /// <summary>
            /// Even though disabled is a global state we still allow the switching of modes via direct calls
            /// to the API.  
            /// </summary>
            [Fact]
            public void SwitchWhenDisabled()
            {
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                _vimBuffer1.SwitchMode(ModeKind.Normal, ModeArgument.None);
                Assert.True(Vim.IsDisabled);
                Assert.Equal(ModeKind.Disabled, _vimBuffer2.ModeKind);
            }

            /// <summary>
            /// Re-enabling with an active selection and 'selectmode='
            /// should enable visual mode and return to normal mode
            /// </summary>
            [Fact]
            public void ReenableActiveSelectionNoSelectModeMouse()
            {
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.Disabled, _vimBuffer1.ModeKind);
                _vimBuffer1.TextView.Selection.Select(0, _vimBuffer1.TextView.GetEndPoint().Position);
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.VisualCharacter, _vimBuffer1.ModeKind);
                _vimBuffer1.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer1.ModeKind);
            }

            /// <summary>
            /// Re-enabling with an active selection and 'selectmode=mouse'
            /// should enable select mode and return to normal mode
            /// </summary>
            [Fact]
            public void ReenableActiveSelectionSelectModeMouse()
            {
                _vimBuffer1.GlobalSettings.SelectMode = "mouse";
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.Disabled, _vimBuffer1.ModeKind);
                _vimBuffer1.TextView.Selection.Select(0, _vimBuffer1.TextView.GetEndPoint().Position);
                _vimBuffer1.Process(GlobalSettings.DisableAllCommand);
                Assert.Equal(ModeKind.SelectCharacter, _vimBuffer1.ModeKind);
                _vimBuffer1.ProcessNotation("<Esc>");
                Assert.Equal(ModeKind.Normal, _vimBuffer1.ModeKind);
            }

            /// <summary>
            /// When we re-enable Vim they buffers which are already out of Disabled Mode should remain in 
            /// whatever mode they are in 
            /// </summary>
            [Fact]
            public void SwitchWhenUndisabled()
            {
                Vim.IsDisabled = true;
                _vimBuffer1.SwitchMode(ModeKind.Insert, ModeArgument.None);
                Vim.IsDisabled = false;
                Assert.Equal(ModeKind.Normal, _vimBuffer2.ModeKind);
                Assert.Equal(ModeKind.Insert, _vimBuffer1.ModeKind);
            }
        }
    }
}
