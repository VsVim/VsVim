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
            var view = EditorUtil.CreateTextView("foo");
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
            _textView = EditorUtil.CreateTextView("");
            _editorOptions = EditorUtil.GetEditorOptions(_textView);
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
    }
}
