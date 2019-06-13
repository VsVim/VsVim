using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using System;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test the synchronization of settings from the IVimLocalSettings to the 
    /// associated IEditorOptions value
    /// </summary>
    public abstract class EditorToSettingSynchronizerTest : VimTestBase
    {
        private readonly Mock<IVimBuffer> _vimBuffer;
        private readonly Mock<IVimTextBuffer> _vimTextBuffer;
        private readonly EditorToSettingSynchronizer _synchronizer;
        private readonly IVimLocalSettings _localSettings;
        private readonly IVimWindowSettings _windowSettings;
        private readonly IEditorOptions _editorOptions;

        public EditorToSettingSynchronizerTest()
        {
            _synchronizer = new EditorToSettingSynchronizer();

            var textView = CreateTextView();
            var globalSettings = new GlobalSettings();
            _localSettings = new LocalSettings(globalSettings);
            _windowSettings = new WindowSettings(globalSettings);
            _editorOptions = textView.Options;

            var vim = new Mock<IVim>(MockBehavior.Strict);
            vim.SetupGet(x => x.VimHost).Returns(VimHost);

            _vimBuffer = new Mock<IVimBuffer>(MockBehavior.Strict);
            _vimBuffer.SetupGet(x => x.LocalSettings).Returns(_localSettings);
            _vimBuffer.SetupGet(x => x.WindowSettings).Returns(_windowSettings);
            _vimBuffer.SetupGet(x => x.TextView).Returns(textView);
            _vimBuffer.SetupGet(x => x.Vim).Returns(vim.Object);
            _vimTextBuffer = new Mock<IVimTextBuffer>(MockBehavior.Strict);
            _vimTextBuffer
                .Setup(x => x.CheckModeLine(It.IsAny<IVimWindowSettings>()))
                .Returns(Tuple.Create(FSharpOption<string>.None, FSharpOption<string>.None));
            _vimBuffer.SetupGet(x => x.Vim).Returns(vim.Object);
            _vimBuffer.SetupGet(x => x.VimTextBuffer).Returns(_vimTextBuffer.Object);
        }

        public sealed class StartSynchronizingTest : EditorToSettingSynchronizerTest
        {
            public StartSynchronizingTest()
            {
                _synchronizer.StartSynchronizing(_vimBuffer.Object, SettingSyncSource.Editor);
            }

            /// <summary>
            /// Verify that it's synchronizing 'tabstop' between the two places
            /// </summary>
            [WpfFact]
            public void TabStop()
            {
                _localSettings.TabStop = 42;
                Assert.Equal(42, _editorOptions.GetOptionValue(DefaultOptions.TabSizeOptionId));

                _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 13);
                Assert.Equal(13, _localSettings.TabStop);
            }

            /// <summary>
            /// Verify that it's synchronizing 'expandtab' between the two places
            /// </summary>
            [WpfFact]
            public void ExpandTab()
            {
                _localSettings.ExpandTab = true;
                Assert.True(_editorOptions.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId));

                _editorOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, false);
                Assert.False(_localSettings.ExpandTab);
            }

            /// <summary>
            /// Verify that it's synchronizing 'shiftwidth' between the two places
            /// </summary>
            [WpfFact]
            public void ShiftWidth()
            {
                _localSettings.ShiftWidth = 42;
                Assert.Equal(42, _editorOptions.GetOptionValue(DefaultOptions.IndentSizeOptionId));

                _editorOptions.SetOptionValue(DefaultOptions.IndentSizeOptionId, 13);
                Assert.Equal(13, _localSettings.ShiftWidth);
            }

            [WpfFact]
            public void WordWrapSimple()
            {
                VimHost.WordWrapStyle = WordWrapStyles.WordWrap;
                _windowSettings.Wrap = true;
                Assert.Equal(WordWrapStyles.WordWrap, _editorOptions.GetOptionValue(DefaultTextViewOptions.WordWrapStyleId));

                _windowSettings.Wrap = false;
                Assert.Equal(WordWrapStyles.None, _editorOptions.GetOptionValue(DefaultTextViewOptions.WordWrapStyleId));
            }

            [WpfFact]
            public void WordWrapReverse()
            {
                _editorOptions.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, WordWrapStyles.WordWrap);
                Assert.True(_windowSettings.Wrap);

                _editorOptions.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, WordWrapStyles.None);
                Assert.False(_windowSettings.Wrap);
            }

            /// <summary>
            /// Word wraps that don't line up with our definition is still a word wrap 
            /// </summary>
            [WpfFact]
            public void WordWrapAutoIndent()
            {
                VimHost.WordWrapStyle = WordWrapStyles.WordWrap;
                _editorOptions.SetOptionValue(DefaultTextViewOptions.WordWrapStyleId, WordWrapStyles.AutoIndent | WordWrapStyles.WordWrap);
                Assert.True(_windowSettings.Wrap);

                Assert.Equal(WordWrapStyles.WordWrap | WordWrapStyles.AutoIndent, _editorOptions.GetOptionValue(DefaultTextViewOptions.WordWrapStyleId));

                _windowSettings.Wrap = false;
                Assert.Equal(WordWrapStyles.None, _editorOptions.GetOptionValue(DefaultTextViewOptions.WordWrapStyleId));
            }
        }

        public sealed class CopyTest : EditorToSettingSynchronizerTest
        {
            [WpfFact]
            public void CopyVimToEditorSettingsTest()
            {
                _localSettings.TabStop = 10;
                _localSettings.ShiftWidth = 11;
                _synchronizer.CopyVimToEditorSettings(_vimBuffer.Object);
                Assert.Equal(10, _editorOptions.GetOptionValue(DefaultOptions.TabSizeOptionId));
                Assert.Equal(11, _editorOptions.GetOptionValue(DefaultOptions.IndentSizeOptionId));
            }

            [WpfFact]
            public void CopyEditorToVimSettingsTest()
            {
                _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 12);
                _editorOptions.SetOptionValue(DefaultOptions.IndentSizeOptionId, 13);
                _synchronizer.CopyEditorToVimSettings(_vimBuffer.Object);
                Assert.Equal(12, _localSettings.TabStop);
                Assert.Equal(13, _localSettings.ShiftWidth);
            }
        }
    }
}
