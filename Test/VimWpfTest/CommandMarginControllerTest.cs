using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Classification;
using Moq;
using Xunit;
using Vim.Extensions;
using Vim.UI.Wpf.Implementation.CommandMargin;
using Vim.UI.Wpf.Properties;
using Vim.UnitTest.Mock;
using Vim.UnitTest;
using Vim.UnitTest.Exports;

namespace Vim.UI.Wpf.UnitTest
{
    using Resources = global::Vim.UI.Wpf.Properties.Resources;

    public abstract class CommandMarginControllerTest : VimTestBase
    {
        private readonly MockRepository _factory;
        private readonly CommandMarginControl _marginControl;
        private readonly CommandMarginController _controller;
        private readonly MockVimBuffer _vimBuffer;
        private readonly Mock<IIncrementalSearch> _search;
        private readonly TestableClipboardDevice _clipboardDevice;
        private Mock<IVimGlobalSettings> _globalSettings;

        protected CommandMarginControllerTest()
        {
            _factory = new MockRepository(MockBehavior.Strict);
            _marginControl = new CommandMarginControl();
            _marginControl.CommandLineTextBox.Text = string.Empty;

            _search = _factory.Create<IIncrementalSearch>();
            _search.SetupGet(x => x.HasActiveSession).Returns(false);
            _search.SetupGet(x => x.InPasteWait).Returns(false);
            _vimBuffer = new MockVimBuffer
            {
                IncrementalSearchImpl = _search.Object,
                VimImpl = MockObjectFactory.CreateVim(factory: _factory).Object,
                CommandModeImpl = _factory.Create<ICommandMode>(MockBehavior.Loose).Object
            };
            var textBuffer = CreateTextBuffer(new[] { "" });
            _vimBuffer.TextViewImpl = TextEditorFactoryService.CreateTextView(textBuffer);

            var vimBufferData = CreateVimBufferData(_vimBuffer.TextView);

            _globalSettings = new Mock<IVimGlobalSettings>();
            _vimBuffer.GlobalSettingsImpl = _globalSettings.Object;

            var editorFormatMap = _factory.Create<IEditorFormatMap>(MockBehavior.Loose);
            editorFormatMap.Setup(x => x.GetProperties(It.IsAny<string>())).Returns(new ResourceDictionary());

            var parentVisualElement = _factory.Create<FrameworkElement>();

            _clipboardDevice = (TestableClipboardDevice)CompositionContainer.GetExportedValue<IClipboardDevice>();

            _controller = new CommandMarginController(
                _vimBuffer,
                parentVisualElement.Object,
                _marginControl,
                VimEditorHost.EditorFormatMapService.GetEditorFormatMap(_vimBuffer.TextView),
                VimEditorHost.ClassificationFormatMapService.GetClassificationFormatMap(_vimBuffer.TextView),
                CommonOperationsFactory.GetCommonOperations(vimBufferData),
                _clipboardDevice);
        }

        public sealed class InCommandLineUpdateTest : CommandMarginControllerTest
        {
            [WpfFact]
            public void Check()
            {
                Assert.False(_controller.InCommandLineUpdate);
                var check = false;
                _marginControl.CommandLineTextBox.TextChanged += delegate
                {
                    Assert.True(_controller.InCommandLineUpdate);
                    check = true;
                };

                _vimBuffer.RaiseErrorMessage("blah");
                Assert.True(check);
                Assert.False(_controller.InCommandLineUpdate);
            }
        }

        public sealed class KeyInputEventTest : CommandMarginControllerTest
        {
            private static KeyInput GetKeyInput(char c)
            {
                return KeyInputUtil.CharToKeyInput(c);
            }

            [WpfFact]
            public void InKeyEvent()
            {
                var keyInput = GetKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(keyInput);
                _vimBuffer.ModeImpl = _vimBuffer.CommandModeImpl;
                Assert.True(_controller.InVimBufferKeyEvent);
                _vimBuffer.RaiseKeyInputEnd(keyInput);
                Assert.False(_controller.InVimBufferKeyEvent);
            }

            [WpfFact]
            public void MessageEventNoKeyEvent()
            {
                var msg = "test";
                _vimBuffer.RaiseErrorMessage(msg);
                Assert.Equal(msg, _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void MessageEventKeyEvent()
            {
                var msg = "test";
                var keyInput = GetKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(keyInput);
                _vimBuffer.RaiseErrorMessage(msg);
                Assert.NotEqual(msg, _marginControl.CommandLineTextBox.Text);
                _vimBuffer.RaiseKeyInputEnd(keyInput);
                Assert.Equal(msg, _marginControl.CommandLineTextBox.Text);
            }
        }

        public sealed class MiscTest : CommandMarginControllerTest
        {
            public MiscTest()
            {
            }

            private void SimulateKeystroke()
            {
                var ki = KeyInputUtil.CharToKeyInput('a');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseKeyInputEnd(ki);
            }

            private void SimulateSearch(string pattern, SearchKind searchKind = null, SearchOptions searchOptions = SearchOptions.None)
            {
                searchKind = searchKind ?? SearchKind.Forward;

                var data = new SearchData(pattern, SearchOffsetData.None, searchKind, searchOptions);
                _search.SetupGet(x => x.HasActiveSession).Returns(true).Verifiable();
                _search.SetupGet(x => x.CurrentSearchData).Returns(data).Verifiable();
                _search.SetupGet(x => x.CurrentSearchText).Returns(pattern).Verifiable();
            }

            /// <summary>
            /// A switch mode with no messages should display the banner
            /// </summary>
            [WpfFact]
            public void SwitchMode1()
            {
                var mode = new Mock<INormalMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
                _vimBuffer.NormalModeImpl = mode.Object;
                _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(_vimBuffer.NormalMode, _vimBuffer.NormalModeImpl));
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// A switch mode with no messages should display the banner
            /// </summary>
            [WpfFact]
            public void SwitchMode2()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(_vimBuffer.NormalMode, mode.Object));
                Assert.Equal(Resources.InsertBanner, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Status line shouldn't be updated until a KeyInput event completes
            /// </summary>
            [WpfFact]
            public void SwitchMode3()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                _marginControl.CommandLineTextBox.Text = string.Empty;
                _vimBuffer.RaiseKeyInputStart(KeyInputUtil.CharToKeyInput('c'));
                _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(_vimBuffer.NormalMode, mode.Object));
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Status line shouldn't be updated until a KeyInput event completes
            /// </summary>
            [WpfFact]
            public void SwitchMode4()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                var ki = KeyInputUtil.CharToKeyInput('c');
                _marginControl.CommandLineTextBox.Text = string.Empty;
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseSwitchedMode(new SwitchModeEventArgs(_vimBuffer.NormalMode, mode.Object));
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal(Resources.InsertBanner, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// A switch to command mode should start the status bar with a :.
            /// </summary>
            [WpfFact]
            public void SwitchMode5()
            {
                var mode = new Mock<ICommandMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
                mode.SetupGet(x => x.Command).Returns("");
                _vimBuffer.CommandModeImpl = mode.Object;
                _vimBuffer.RaiseSwitchedMode(_vimBuffer.CommandModeImpl);
                Assert.Equal(":", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// A switch to command mode should start the status bar with a :. + the command
            /// </summary>
            [WpfFact]
            public void SwitchMode6()
            {
                var mode = new Mock<ICommandMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
                mode.SetupGet(x => x.Command).Returns("foo");
                _vimBuffer.CommandModeImpl = mode.Object;
                _vimBuffer.RaiseSwitchedMode(_vimBuffer.CommandModeImpl);
                Assert.Equal(":foo", _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void SwitchMode7()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Replace);
                _vimBuffer.RaiseSwitchedMode(mode.Object);
                Assert.Equal(Resources.ReplaceBanner, _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void SwitchMode_OneTimeCommand_Insert()
            {
                var mode = new Mock<INormalMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
                _vimBuffer.InOneTimeCommandImpl = FSharpOption.Create(ModeKind.Insert);
                _vimBuffer.NormalModeImpl = mode.Object;
                _vimBuffer.RaiseSwitchedMode(mode.Object);
                Assert.Equal(string.Format(Resources.NormalOneTimeCommandBanner, "insert"), _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void SwitchMode_SubstituteConfirm1()
            {
                var mode = _factory.Create<ISubstituteConfirmMode>();
                mode.SetupGet(x => x.CurrentSubstitute).Returns(FSharpOption.Create("here")).Verifiable();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.SubstituteConfirm).Verifiable();
                _vimBuffer.SubstituteConfirmModeImpl = mode.Object;
                _vimBuffer.RaiseSwitchedMode(mode.Object);

                Assert.Equal(string.Format(Resources.SubstituteConfirmBannerFormat, "here"), _marginControl.CommandLineTextBox.Text);
                _factory.Verify();
            }

            [WpfFact]
            public void StatusMessage1()
            {
                _vimBuffer.RaiseStatusMessage("foo");
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't add it until the end of a KeyInput event
            /// </summary>
            [WpfFact]
            public void StatusMessage2()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo");
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't add it until the end of a KeyInput event
            /// </summary>
            [WpfFact]
            public void StatusMessage3()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo");
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode
            /// </summary>
            [WpfFact]
            public void StatusMessage4()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo");
                _vimBuffer.RaiseSwitchedMode(mode.Object);
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void StatusMessage5()
            {
                _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
                Assert.Equal("foo" + Environment.NewLine + "bar", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't update in the middle of an KeyInput event
            /// </summary>
            [WpfFact]
            public void StatusMessage6()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't update in the middle of an KeyInput event
            /// </summary>
            [WpfFact]
            public void StatusMessage7()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo" + Environment.NewLine + "bar", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// StatusMessageLong wins over SwitchMode
            /// </summary>
            [WpfFact]
            public void StatusMessage8()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseStatusMessage("foo" + Environment.NewLine + "bar");
                _vimBuffer.RaiseSwitchedMode(mode.Object);
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo" + Environment.NewLine + "bar", _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void ErrorMessage1()
            {
                _vimBuffer.RaiseErrorMessage("foo");
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't add it until the end of a KeyInput event
            /// </summary>
            [WpfFact]
            public void ErrorMessage2()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseErrorMessage("foo");
                Assert.Equal(string.Empty, _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Don't add it until the end of a KeyInput event
            /// </summary>
            [WpfFact]
            public void ErrorMessage3()
            {
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseErrorMessage("foo");
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            /// <summary>
            /// Status message should win over mode switch.  Think :setting ignorecase.  Both status + switch mode
            /// </summary>
            [WpfFact]
            public void ErrorMessage4()
            {
                var mode = new Mock<IMode>();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Insert);
                var ki = KeyInputUtil.CharToKeyInput('c');
                _vimBuffer.RaiseKeyInputStart(ki);
                _vimBuffer.RaiseErrorMessage("foo");
                _vimBuffer.RaiseSwitchedMode(mode.Object);
                _vimBuffer.RaiseKeyInputEnd(ki);
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
            }

            [WpfFact]
            public void NoEvents1()
            {
                var mode = new Mock<INormalMode>();
                var runner = new Mock<ICommandRunner>();
                mode.SetupGet(x => x.CommandRunner).Returns(runner.Object);
                runner.SetupGet(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
                _globalSettings.SetupGet(x => x.ShowCommand).Returns(true);
                _search.SetupGet(x => x.HasActiveSession).Returns(false).Verifiable();
                mode.SetupGet(x => x.Command).Returns("foo");
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Normal);
                _vimBuffer.BufferedKeyInputsImpl = FSharpList<KeyInput>.Empty;
                _vimBuffer.ModeKindImpl = ModeKind.Normal;
                _vimBuffer.ModeImpl = mode.Object;
                _vimBuffer.NormalModeImpl = mode.Object;

                SimulateKeystroke();
                Assert.Equal("foo", _marginControl.ShowCommandText.Text);
                mode.Verify();
                _factory.Verify();
            }

            [WpfFact]
            public void NoEvents2()
            {
                var mode = new Mock<ICommandMode>();
                mode.SetupGet(x => x.Command).Returns("foo");
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Command);
                _vimBuffer.ModeKindImpl = ModeKind.Command;
                _vimBuffer.ModeImpl = mode.Object;
                _vimBuffer.CommandModeImpl = mode.Object;

                SimulateKeystroke();
                Assert.Equal(":foo", _marginControl.CommandLineTextBox.Text);
                mode.Verify();
            }

            [WpfFact]
            public void NoEvents3()
            {
                var mode = new Mock<IDisabledMode>();
                mode.SetupGet(x => x.HelpMessage).Returns("foo").Verifiable();
                mode.SetupGet(x => x.ModeKind).Returns(ModeKind.Disabled);
                _vimBuffer.ModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.Disabled;
                _vimBuffer.DisabledModeImpl = mode.Object;

                SimulateKeystroke();
                Assert.Equal("foo", _marginControl.CommandLineTextBox.Text);
                mode.Verify();
            }

            /// <summary>
            /// Ensure the status line is updated for a normal mode search
            /// </summary>
            [WpfFact]
            public void Search_Normal_Forward()
            {
                var mode = _factory.Create<INormalMode>();
                _vimBuffer.NormalModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.Normal;
                SimulateSearch("cat");
                SimulateKeystroke();
                Assert.Equal("/cat", _marginControl.CommandLineTextBox.Text);
                _factory.Verify();
            }

            [WpfFact]
            public void Search_Normal_Backward()
            {
                var mode = _factory.Create<INormalMode>();
                _vimBuffer.NormalModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.Normal;
                SimulateSearch("cat", SearchKind.Backward);
                SimulateKeystroke();
                Assert.Equal("?cat", _marginControl.CommandLineTextBox.Text);
                _factory.Verify();
            }

            /// <summary>
            /// Ensure the status line is updated for a visual mode search
            /// </summary>
            [WpfFact]
            public void Search_Visual_Forward()
            {
                var mode = _factory.Create<IVisualMode>();
                _vimBuffer.VisualCharacterModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
                SimulateSearch("cat");
                SimulateKeystroke();
                Assert.Equal("/cat", _marginControl.CommandLineTextBox.Text);
                _factory.Verify();
            }

            [WpfFact]
            public void Search_Visual_Backward()
            {
                var mode = _factory.Create<IVisualMode>();
                _vimBuffer.VisualCharacterModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
                SimulateSearch("cat", SearchKind.Backward);
                SimulateKeystroke();
                Assert.Equal("?cat", _marginControl.CommandLineTextBox.Text);
                _factory.Verify();
            }

            /// <summary>
            /// Once a visual search is complete we should go back to the standard
            /// visual mode banner
            /// </summary>
            [WpfFact]
            public void Search_Visual_Complete()
            {
                var mode = _factory.Create<IVisualMode>();
                var runner = _factory.Create<ICommandRunner>(MockBehavior.Loose);
                mode.Setup(x => x.CommandRunner).Returns(runner.Object);
                runner.Setup(x => x.Inputs).Returns(FSharpList<KeyInput>.Empty);
                mode.Setup(x => x.ModeKind).Returns(ModeKind.VisualCharacter);
                _vimBuffer.VisualCharacterModeImpl = mode.Object;
                _vimBuffer.ModeKindImpl = ModeKind.VisualCharacter;
                _vimBuffer.ModeImpl = mode.Object;
                SimulateSearch("cat", SearchKind.Backward);
                SimulateKeystroke();
                _search.SetupGet(x => x.HasActiveSession).Returns(false);
                SimulateKeystroke();
                Assert.Equal(Resources.VisualCharacterBanner, _marginControl.CommandLineTextBox.Text);
            }
        }
    }
}
