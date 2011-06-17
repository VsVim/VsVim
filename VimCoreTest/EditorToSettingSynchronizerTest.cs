using Microsoft.VisualStudio.Text.Editor;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;

namespace VimCore.UnitTest
{
    /// <summary>
    /// Test the synchronization of settings from the IVimLocalSettings to the 
    /// associated IEditorOptions value
    /// </summary>
    [TestFixture]
    public sealed class EditorToSettingSynchronizerTest
    {
        private EditorToSettingSynchronizer _synchronizer;
        private IVimBuffer _buffer;
        private IVimGlobalSettings _globalSettings;
        private IVimLocalSettings _localSettings;
        private IEditorOptions _editorOptions;

        [SetUp]
        public void Setup()
        {
            _synchronizer = new EditorToSettingSynchronizer(EditorUtil.FactoryService.Vim);
            _buffer = EditorUtil.FactoryService.Vim.CreateBuffer(EditorUtil.CreateTextView(""));
            _localSettings = _buffer.LocalSettings;
            _globalSettings = _localSettings.GlobalSettings;
            _editorOptions = _localSettings.EditorOptions.Value;
        }

        [TearDown]
        public void TearDown()
        {
            _buffer.Close();
        }

        /// <summary>
        /// Verify that it's synchronizing 'tabstop' between the two places
        /// </summary>
        [Test]
        public void Sync_TabStop()
        {
            _localSettings.TabStop = 42;
            Assert.AreEqual(42, _editorOptions.GetOptionValue(DefaultOptions.TabSizeOptionId));

            _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 13);
            Assert.AreEqual(13, _localSettings.TabStop);
        }

        /// <summary>
        /// Verify that it's synchronizing 'expandtab' between the two places
        /// </summary>
        [Test]
        public void Sync_ExpandTab()
        {
            _localSettings.ExpandTab = true;
            Assert.IsTrue(_editorOptions.GetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId));

            _editorOptions.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, false);
            Assert.IsFalse(_localSettings.ExpandTab);
        }
    }
}
