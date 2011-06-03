using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using NUnit.Framework;
using Vim;
using Vim.UnitTest;
using GlobalSettings = Vim.GlobalSettings;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class LocalSettingsTest : SettingsCommonTest
    {
        protected override string ToggleSettingName { get { return LocalSettingNames.NumberName; } }
        protected override IVimSettings Create()
        {
            var global = new Vim.GlobalSettings();
            var view = EditorUtil.CreateView("foo");
            var editorOptions = EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(view);
            return new LocalSettings(global, editorOptions, view);
        }

        private ITextView _textView;
        private IEditorOptions _editorOptions;
        private IVimGlobalSettings _global;
        private LocalSettings _localRaw;
        private IVimLocalSettings _local;

        [SetUp]
        public void SetUp()
        {
            _textView = EditorUtil.CreateView("");
            _editorOptions = EditorUtil.GetOptions(_textView);
            _global = new GlobalSettings();
            _localRaw = new LocalSettings(_global, _editorOptions, _textView);
            _local = _localRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _global = null;
            _localRaw = null;
            _local = null;
        }

        [Test]
        public void Sanity1()
        {
            Assert.AreSame(_global, _local.GlobalSettings);
        }

        /// <summary>
        /// When the UseEditorTabSettings is false we should prefer Vim settings
        /// </summary>
        [Test]
        public void TabStop_UseVim()
        {
            _global.UseEditorTabSettings = false;
            _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 4);
            _local.TabStop = 42;
            Assert.AreEqual(4, _editorOptions.GetTabSize());
            Assert.AreEqual(42, _local.TabStop);
        }

        /// <summary>
        /// When the UseEditorTabSettings is true we should prefer Editor settings
        /// </summary>
        [Test]
        [Ignore("Need to move these test to include the sync class")]
        public void TabStop_UseEditor()
        {
            _global.UseEditorTabSettings = true;
            _editorOptions.SetOptionValue(DefaultOptions.TabSizeOptionId, 42);
            Assert.AreEqual(42, _local.TabStop);
            _local.TabStop = 13;
            Assert.AreEqual(13, _local.TabStop);
            Assert.AreEqual(13, _editorOptions.GetTabSize());
        }

    }
}
