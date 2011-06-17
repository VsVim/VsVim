using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes.Command;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class CommandDefaultOperationsTest
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _textView;
        private MockRepository _factory;
        private Mock<IEditorOperations> _editOpts;
        private Mock<IVimHost> _host;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _localSettings;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IKeyMap> _keyMap;
        private Mock<IOutliningManager> _outlining;
        private Mock<IRegisterMap> _registerMap;
        private IUndoRedoOperations _undoRedoOperations;
        private ISearchService _searchService;

        private void Create(params string[] lines)
        {
            _textView = EditorUtil.CreateTextView(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _editOpts = _factory.Create<IEditorOperations>();
            _editOpts.Setup(x => x.AddAfterTextBufferChangePrimitive());
            _editOpts.Setup(x => x.AddBeforeTextBufferChangePrimitive());
            _host = _factory.Create<IVimHost>();
            _jumpList = _factory.Create<IJumpList>();
            _registerMap = MockObjectFactory.CreateRegisterMap(factory: _factory);
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _globalSettings.SetupGet(x => x.SmartCase).Returns(false);
            _globalSettings.SetupGet(x => x.IgnoreCase).Returns(true);
            _localSettings = MockObjectFactory.CreateLocalSettings(global: _globalSettings.Object);
            _keyMap = _factory.Create<IKeyMap>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _outlining = _factory.Create<IOutliningManager>();
            _undoRedoOperations = VimUtil.CreateUndoRedoOperations(_statusUtil.Object);
            _searchService = VimUtil.CreateSearchService(_globalSettings.Object);

            var vimData = new VimData();
            var data = new OperationsData(
                vimData: vimData,
                vimHost: _host.Object,
                textView: _textView,
                editorOperations: _editOpts.Object,
                outliningManager: FSharpOption.Create(_outlining.Object),
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _localSettings.Object,
                keyMap: _keyMap.Object,
                undoRedoOperations: _undoRedoOperations,
                editorOptions: null,
                foldManager: null,
                registerMap: _registerMap.Object,
                searchService: _searchService,
                wordUtil: VimUtil.GetWordUtil(_textView));
            _operationsRaw = new DefaultOperations(
                new CommonOperations(data),
                _textView,
                _editOpts.Object,
                _jumpList.Object,
                _localSettings.Object,
                _undoRedoOperations,
                _keyMap.Object,
                vimData,
                _host.Object,
                _statusUtil.Object);
            _operations = _operationsRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _operations = null;
        }


        private void AssertPrintMap(string input, string output)
        {
            var keyInputSet = KeyNotationUtil.StringToKeyInputSet(input);
            _keyMap
                .Setup(x => x.GetKeyMappingsForMode(KeyRemapMode.Normal))
                .Returns(new[] { Tuple.Create(keyInputSet, keyInputSet) })
                .Verifiable();

            var expected = String.Format("n    {0} {0}", output);
            _statusUtil
                .Setup(x => x.OnStatusLong(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(x => Assert.AreEqual(expected, x.Single()))
                .Verifiable();
            _operations.PrintKeyMap((new[] { KeyRemapMode.Normal }).ToFSharpList());
            _factory.Verify();
        }

        [Test]
        public void OperateSetting1()
        {
            Create("foO");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(true), SettingValue.NewToggleValue(true), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _localSettings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.OperateSetting("foobar");
            _localSettings.Verify();
        }

        [Test]
        public void OperateSetting2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _localSettings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.OperateSetting("foobar");
            _localSettings.Verify();
        }

        [Test]
        public void OperateSetting3()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewNumberValue(42), SettingValue.NewNumberValue(42), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnStatus(It.IsAny<string>())).Verifiable();
            _operations.OperateSetting("foobar");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void OperateSetting4()
        {
            Create("foo");
            _localSettings.Setup(x => x.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.OperateSetting("foo");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void ResetSettings1()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _localSettings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.ResetSetting("foobar");
            _localSettings.Verify();
        }

        [Test]
        public void ResetSettings2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidArgument("foobar"))).Verifiable();
            _operations.ResetSetting("foobar");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void ResetSettings3()
        {
            Create("foo");
            _localSettings.Setup(x => x.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.ResetSetting("foo");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void InvertSettings1()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _localSettings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.InvertSetting("foobar");
            _localSettings.Verify();
        }

        [Test]
        public void InvertSettings2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidArgument("foobar"))).Verifiable();
            _operations.InvertSetting("foobar");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void InvertSettings3()
        {
            Create("foo");
            _localSettings.Setup(x => x.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.InvertSetting("foo");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintModifiedSettings1()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.AllSettings).Returns(Enumerable.Repeat(setting, 1));
            _statusUtil.Setup(x => x.OnStatusLong(It.IsAny<IEnumerable<string>>())).Verifiable();
            _operations.PrintModifiedSettings();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintAllSettings1()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.AllSettings).Returns(Enumerable.Repeat(setting, 1));
            _statusUtil.Setup(x => x.OnStatusLong(It.IsAny<IEnumerable<string>>())).Verifiable();
            _operations.PrintAllSettings();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting1()
        {
            Create("foobar");
            _localSettings.Setup(x => x.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.PrintSetting("foo");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting2()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("nofoobar")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting3()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(true), SettingValue.NewToggleValue(true), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("foobar")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting4()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewNumberValue(42), SettingValue.NewNumberValue(42), false);
            _localSettings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("foobar=42")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void SetSettingValue1()
        {
            Create("foobar");
            _localSettings.Setup(x => x.TrySetValueFromString("foo", "bar")).Returns(true).Verifiable();
            _operations.SetSettingValue("foo", "bar");
            _localSettings.Verify();
        }

        [Test]
        public void SetSettingValue2()
        {
            Create("foobar");
            _localSettings.Setup(x => x.TrySetValueFromString("foo", "bar")).Returns(false).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidValue("foo", "bar"))).Verifiable();
            _operations.SetSettingValue("foo", "bar");
            _localSettings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void RemapKeys1()
        {
            Create("foo");
            _keyMap.Setup(x => x.MapWithRemap("foo", "bar", KeyRemapMode.Insert)).Returns(true).Verifiable();
            _operations.RemapKeys("foo", "bar", Enumerable.Repeat(KeyRemapMode.Insert, 1), true);
            _keyMap.Verify();
        }

        [Test]
        public void RemapKeys2()
        {
            Create("foo");
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_NotSupported_KeyMapping("a", "b"))).Verifiable();
            _keyMap.Setup(x => x.MapWithNoRemap("a", "b", KeyRemapMode.Insert)).Returns(false).Verifiable();
            _operations.RemapKeys("a", "b", Enumerable.Repeat(KeyRemapMode.Insert, 1), false);
            _statusUtil.Verify();
            _keyMap.Verify();
        }

        [Test]
        public void RemapKeys3()
        {
            Create("foo");
            _keyMap.Setup(x => x.MapWithNoRemap("a", "b", KeyRemapMode.Insert)).Returns(true).Verifiable();
            _operations.RemapKeys("a", "b", Enumerable.Repeat(KeyRemapMode.Insert, 1), false);
            _keyMap.Verify();
        }

        /// <summary>
        /// Standard replace of tabs with the associated set of spaces
        /// </summary>
        [Test]
        public void Retab_Simple()
        {
            Create("\thello\tworld");
            _localSettings.SetupGet(x => x.TabStop).Returns(4);
            _localSettings.SetupGet(x => x.ExpandTab).Returns(true);
            _operationsRaw.RetabLineRange(_textView.GetLineRange(0), includeSpaces: false);
            Assert.AreEqual("    hello    world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Don't replace space strings if it's not specified
        /// </summary>
        [Test]
        public void Retab_NoSpaces()
        {
            Create("    hello    world");
            _localSettings.SetupGet(x => x.TabStop).Returns(4);
            _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
            _operationsRaw.RetabLineRange(_textView.GetLineRange(0), includeSpaces: false);
            Assert.AreEqual("    hello    world", _textView.GetLine(0).GetText());
        }

        /// <summary>
        /// Replace spaces if they are specified
        /// </summary>
        [Test]
        public void Retab_Spaces()
        {
            Create("    hello    world");
            _localSettings.SetupGet(x => x.TabStop).Returns(4);
            _localSettings.SetupGet(x => x.ExpandTab).Returns(false);
            _operationsRaw.RetabLineRange(_textView.GetLineRange(0), includeSpaces: true);
            Assert.AreEqual("\thello\tworld", _textView.GetLine(0).GetText());
        }

        [Test]
        public void UnmapKeys1()
        {
            Create("foo");
            _keyMap.Setup(x => x.Unmap("h", KeyRemapMode.Insert)).Returns(false).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_NoSuchMapping)).Verifiable();
            _operations.UnmapKeys("h", Enumerable.Repeat(KeyRemapMode.Insert, 1));
            _keyMap.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void UnmapKeys2()
        {
            Create("foo");
            _keyMap.Setup(x => x.Unmap("h", KeyRemapMode.Insert)).Returns(true).Verifiable();
            _operations.UnmapKeys("h", Enumerable.Repeat(KeyRemapMode.Insert, 1));
            _keyMap.Verify();
        }

        [Test]
        public void PrintKeyMap_LowerAlpha()
        {
            Create("foo");
            AssertPrintMap("a", "a");
            AssertPrintMap("b", "b");
        }

        [Test]
        public void PrintKeyMap_UpperAplha()
        {
            Create("foo");
            AssertPrintMap("A", "A");
            AssertPrintMap("<S-a>", "A");
            AssertPrintMap("<S-A>", "A");
        }

        [Test]
        public void PrintKeyMap_AlphaWithControl()
        {
            Create("foo");
            AssertPrintMap("<c-a>", "<C-A>");
            AssertPrintMap("<c-S-a>", "<C-A>");
        }

        [Test]
        public void PrintKeyMap_SpecialKey()
        {
            Create("foo");
            AssertPrintMap("<Esc>", "<Esc>");
            AssertPrintMap("<c-[>", "<Esc>");
            AssertPrintMap("<c-@>", "<Nul>");
            AssertPrintMap("<Tab>", "<Tab>");
            AssertPrintMap("<c-i>", "<Tab>");
            AssertPrintMap("<c-h>", "<C-H>");
            AssertPrintMap("<BS>", "<BS>");
            AssertPrintMap("<NL>", "<NL>");
            AssertPrintMap("<c-j>", "<NL>");
            AssertPrintMap("<c-l>", "<C-L>");
            AssertPrintMap("<FF>", "<FF>");
            AssertPrintMap("<c-m>", "<CR>");
            AssertPrintMap("<CR>", "<CR>");
            AssertPrintMap("<Return>", "<CR>");
            AssertPrintMap("<Enter>", "<CR>");
        }
    }
}
