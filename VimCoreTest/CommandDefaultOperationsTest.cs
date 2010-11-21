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
using Vim.Modes;
using Vim.Modes.Command;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.Test
{
    [TestFixture]
    public class CommandDefaultOperationsTest
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _view;
        private MockRepository _factory;
        private Mock<IEditorOperations> _editOpts;
        private Mock<IVimHost> _host;
        private Mock<IStatusUtil> _statusUtil;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IKeyMap> _keyMap;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IRegisterMap> _registerMap;

        private void Create(params string[] lines)
        {
            _view = EditorUtil.CreateView(lines);
            _factory = new MockRepository(MockBehavior.Strict);
            _editOpts = _factory.Create<IEditorOperations>();
            _host = _factory.Create<IVimHost>();
            _jumpList = _factory.Create<IJumpList>();
            _registerMap = MockObjectFactory.CreateRegisterMap(factory: _factory);
            _globalSettings = _factory.Create<IVimGlobalSettings>();
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _globalSettings.SetupGet(x => x.SmartCase).Returns(false);
            _globalSettings.SetupGet(x => x.IgnoreCase).Returns(true);
            _settings = MockObjectFactory.CreateLocalSettings(global: _globalSettings.Object);
            _keyMap = _factory.Create<IKeyMap>();
            _statusUtil = _factory.Create<IStatusUtil>();
            _outlining = _factory.Create<IOutliningManager>();
            _undoRedoOperations = _factory.Create<IUndoRedoOperations>();

            var data = new OperationsData(
                vimHost: _host.Object,
                textView: _view,
                editorOperations: _editOpts.Object,
                outliningManager: _outlining.Object,
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                keyMap: _keyMap.Object,
                undoRedoOperations: _undoRedoOperations.Object,
                editorOptions: null,
                navigator: null,
                foldManager: null,
                registerMap: _registerMap.Object);
            _operationsRaw = new DefaultOperations(data);
            _operations = _operationsRaw;
        }

        [TearDown]
        public void TearDown()
        {
            _operations = null;
        }

        [Test]
        public void Put1()
        {
            Create("foo");
            _operations.Put("bar", _view.TextSnapshot.GetLineFromLineNumber(0), false);
        }

        [Test]
        public void Put2()
        {
            Create("bar", "baz");
            _operations.Put(" here", _view.TextSnapshot.GetLineFromLineNumber(0), true);
            var tss = _view.TextSnapshot;
            Assert.AreEqual("bar", tss.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual(" here", tss.GetLineFromLineNumber(1).GetText());
            Assert.AreEqual(tss.GetLineFromLineNumber(1).Start.Add(1).Position, _view.Caret.Position.BufferPosition.Position);
        }

        [Test, Description("Only once per line")]
        public void Substitute1()
        {
            Create("bar bar", "foo");
            _operations.Substitute("bar", "again", _view.GetLineRange(0), SubstituteFlags.None);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
        }

        [Test, Description("Should run on every line in the span")]
        public void Substitute2()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 2))).Verifiable();
            _operations.Substitute("bar", "again", _view.GetLineRange(0, 1), SubstituteFlags.None);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo again", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Replace all if the option is set")]
        public void Substitute3()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("bar", "again", _view.GetLineRange(0), SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            Assert.AreEqual("foo bar", _view.TextSnapshot.GetLineFromLineNumber(1).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Ignore case")]
        public void Substitute4()
        {
            Create("bar bar", "foo bar");
            _operations.Substitute("BAR", "again", _view.GetLineRange(0), SubstituteFlags.IgnoreCase);
            Assert.AreEqual("again bar", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute5()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("BAR", "again", _view.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("Ignore case and replace all")]
        public void Substitute6()
        {
            Create("bar bar", "foo bar");
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("BAR", "again", _view.GetLineRange(0), SubstituteFlags.IgnoreCase | SubstituteFlags.ReplaceAll);
            Assert.AreEqual("again again", _view.TextSnapshot.GetLineFromLineNumber(0).GetText());
            _statusUtil.Verify();
        }

        [Test, Description("No matches")]
        public void Substitute7()
        {
            Create("bar bar", "foo bar");
            var pattern = "BAR";
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_PatternNotFound(pattern))).Verifiable();
            _operations.Substitute("BAR", "again", _view.GetLineRange(0), SubstituteFlags.OrdinalCase);
            _statusUtil.Verify();
        }

        [Test, Description("Invalid regex")]
        public void Substitute8()
        {
            Create("bar bar", "foo bar");
            var original = _view.TextSnapshot;
            var pattern = "(foo";
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_PatternNotFound(pattern))).Verifiable();
            _operations.Substitute(pattern, "again", _view.GetLineRange(0), SubstituteFlags.OrdinalCase);
            _statusUtil.Verify();
            Assert.AreSame(original, _view.TextSnapshot);
        }

        [Test, Description("Report only shouldn't make any changes")]
        public void Substitute9()
        {
            Create("bar bar", "foo bar");
            var tss = _view.TextSnapshot;
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 1))).Verifiable();
            _operations.Substitute("bar", "again", _view.GetLineRange(0), SubstituteFlags.ReplaceAll | SubstituteFlags.ReportOnly);
            _statusUtil.Verify();
            Assert.AreSame(tss, _view.TextSnapshot);
        }

        [Test, Description("No matches and report only")]
        public void Substitute10()
        {
            Create("bar bar", "foo bar");
            var tss = _view.TextSnapshot;
            var pattern = "BAR";
            _operations.Substitute(pattern, "again", _view.GetLineRange(0), SubstituteFlags.OrdinalCase | SubstituteFlags.ReportOnly);
        }

        [Test]
        [Description("Across multiple lines one match per line should be processed")]
        public void Substitute11()
        {
            Create("cat", "bat");
            _statusUtil.Setup(x => x.OnStatus(Resources.CommandMode_SubstituteComplete(2, 2))).Verifiable();
            _operations.Substitute("a", "o", _view.GetLineRange(0, 1), SubstituteFlags.None);
            Assert.AreEqual("cot", _view.GetLine(0).GetText());
            Assert.AreEqual("bot", _view.GetLine(1).GetText());
        }

        [Test]
        public void OperateSetting1()
        {
            Create("foO");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(true), SettingValue.NewToggleValue(true), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _settings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.OperateSetting("foobar");
            _settings.Verify();
        }

        [Test]
        public void OperateSetting2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _settings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.OperateSetting("foobar");
            _settings.Verify();
        }

        [Test]
        public void OperateSetting3()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewNumberValue(42), SettingValue.NewNumberValue(42), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnStatus(It.IsAny<string>())).Verifiable();
            _operations.OperateSetting("foobar");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void OperateSetting4()
        {
            Create("foo");
            _settings.Setup(X => X.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.OperateSetting("foo");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void ResetSettings1()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _settings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.ResetSetting("foobar");
            _settings.Verify();
        }

        [Test]
        public void ResetSettings2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidArgument("foobar"))).Verifiable();
            _operations.ResetSetting("foobar");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void ResetSettings3()
        {
            Create("foo");
            _settings.Setup(X => X.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.ResetSetting("foo");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void InvertSettings1()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _settings.Setup(x => x.TrySetValue("foobar", It.IsAny<SettingValue>())).Returns(true).Verifiable();
            _operations.InvertSetting("foobar");
            _settings.Verify();
        }

        [Test]
        public void InvertSettings2()
        {
            Create("foo");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting)).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidArgument("foobar"))).Verifiable();
            _operations.InvertSetting("foobar");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void InvertSettings3()
        {
            Create("foo");
            _settings.Setup(X => X.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.InvertSetting("foo");
            _settings.Verify();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintModifiedSettings1()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.AllSettings).Returns(Enumerable.Repeat(setting, 1));
            _statusUtil.Setup(x => x.OnStatusLong(It.IsAny<IEnumerable<string>>())).Verifiable();
            _operations.PrintModifiedSettings();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintAllSettings1()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.AllSettings).Returns(Enumerable.Repeat(setting, 1));
            _statusUtil.Setup(x => x.OnStatusLong(It.IsAny<IEnumerable<string>>())).Verifiable();
            _operations.PrintAllSettings();
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting1()
        {
            Create("foobar");
            _settings.Setup(x => x.GetSetting("foo")).Returns(FSharpOption<Setting>.None).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_UnknownOption("foo"))).Verifiable();
            _operations.PrintSetting("foo");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting2()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(false), SettingValue.NewToggleValue(false), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("nofoobar")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting3()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.ToggleKind, SettingValue.NewToggleValue(true), SettingValue.NewToggleValue(true), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("foobar")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void PrintSetting4()
        {
            Create("foobar");
            var setting = new Setting("foobar", "fb", SettingKind.NumberKind, SettingValue.NewNumberValue(42), SettingValue.NewNumberValue(42), false);
            _settings.Setup(x => x.GetSetting("foobar")).Returns(FSharpOption.Create(setting));
            _statusUtil.Setup(x => x.OnStatus("foobar=42")).Verifiable();
            _operations.PrintSetting("foobar");
            _statusUtil.Verify();
        }

        [Test]
        public void SetSettingValue1()
        {
            Create("foobar");
            _settings.Setup(x => x.TrySetValueFromString("foo", "bar")).Returns(true).Verifiable();
            _operations.SetSettingValue("foo", "bar");
            _settings.Verify();
        }

        [Test]
        public void SetSettingValue2()
        {
            Create("foobar");
            _settings.Setup(x => x.TrySetValueFromString("foo", "bar")).Returns(false).Verifiable();
            _statusUtil.Setup(x => x.OnError(Resources.CommandMode_InvalidValue("foo", "bar"))).Verifiable();
            _operations.SetSettingValue("foo", "bar");
            _settings.Verify();
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
    }
}
