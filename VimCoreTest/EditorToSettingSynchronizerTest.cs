using Microsoft.VisualStudio.Text.Editor;
using Xunit;

namespace Vim.UnitTest
{
    /// <summary>
    /// Test the synchronization of settings from the IVimLocalSettings to the 
    /// associated IEditorOptions value
    /// </summary>
    public sealed class EditorToSettingSynchronizerTest : VimTestBase
    {
        private readonly EditorToSettingSynchronizer _synchronizer;
        private readonly IVimBuffer _buffer;
        private readonly IVimGlobalSettings _globalSettings;
        private readonly IVimLocalSettings _localSettings;
        private readonly IEditorOptions _editorOptions;

        public EditorToSettingSynchronizerTest()
        {
            _synchronizer = new EditorToSettingSynchronizer(EditorOptionsFactoryService, Vim);

            _buffer = CreateVimBuffer("");
            _localSettings = _buffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _editorOptions = _buffer.TextView.Options;
        }

        /// <summary>
        /// Verify that it's synchronizing 'tabstop' between the two places
        /// </summary>
        [Fact]
        public void Sync_TabStop()
        {
            _localSettings.TabStop = 42;
            Assert.Equal(42, _editorOptions.GetOptionValue(DefaultOptions.TabSizeOptionId));

            _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 13);
            Assert.Equal(13, _localSettings.TabStop);
        }

        /// <summary>
        /// Verify that it's synchronizing 'expandtab' between the two places
        /// </summary>
        [Fact]
        public void Sync_ExpandTab()
        {
            _localSettings.ExpandTab = true;
            Assert.True(_editorOptions.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId));

            _editorOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, false);
            Assert.False(_localSettings.ExpandTab);
        }
    }
}
