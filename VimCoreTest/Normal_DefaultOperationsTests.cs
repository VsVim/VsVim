using System;
using System.Collections.Generic;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.Modes;
using Vim.Modes.Normal;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class Normal_DefaultOperationsTests
    {
        private IOperations _operations;
        private DefaultOperations _operationsRaw;
        private ITextView _textView;
        private Mock<IEditorOptions> _bufferOptions;
        private Mock<IVimHost> _host;
        private Mock<IJumpList> _jumpList;
        private Mock<IVimGlobalSettings> _globalSettings;
        private Mock<IVimLocalSettings> _settings;
        private Mock<IOutliningManager> _outlining;
        private Mock<IUndoRedoOperations> _undoRedoOperations;
        private Mock<IEditorOptions> _options;
        private Mock<IRegisterMap> _registerMap;
        private ISearchService _searchService;
        private IVimData _vimData;
        private Mock<IStatusUtil> _statusUtil;

        private void Create(params string[] lines)
        {
            Create(null, null, lines);
        }

        private void Create(
            IEditorOperations editorOpts = null,
            ITextStructureNavigator baseNav = null,
            params string[] lines)
        {
            _host = new Mock<IVimHost>(MockBehavior.Strict);
            if (editorOpts == null)
            {
                var tuple = EditorUtil.CreateViewAndOperations(lines);
                _textView = tuple.Item1;
                editorOpts = tuple.Item2;
            }
            else
            {
                _textView = EditorUtil.CreateView(lines);
            }

            var editorOptions = EditorUtil.FactoryService.EditorOptionsFactory.GetOptions(_textView);
            baseNav = baseNav ?? (new Mock<ITextStructureNavigator>(MockBehavior.Strict)).Object;
            var nav = TssUtil.CreateTextStructureNavigator(WordKind.NormalWord, baseNav);
            _vimData = new VimData();
            _bufferOptions = new Mock<IEditorOptions>(MockBehavior.Strict);
            _bufferOptions.Setup(x => x.GetOptionValue(DefaultOptions.TabSizeOptionId)).Returns(4);
            _globalSettings = MockObjectFactory.CreateGlobalSettings(ignoreCase: true);
            _globalSettings.SetupGet(x => x.Magic).Returns(true);
            _globalSettings.SetupGet(x => x.IgnoreCase).Returns(true);
            _globalSettings.SetupGet(x => x.SmartCase).Returns(false);
            _settings = MockObjectFactory.CreateLocalSettings(_globalSettings.Object);
            _options = new Mock<IEditorOptions>(MockBehavior.Strict);
            _options.Setup(x => x.GetOptionValue<int>(It.IsAny<string>())).Throws(new ArgumentException());
            _options.Setup(x => x.GetOptionValue<int>(It.IsAny<EditorOptionKey<int>>())).Throws(new ArgumentException());
            _options.Setup(x => x.IsOptionDefined<int>(It.IsAny<EditorOptionKey<int>>(), false)).Returns(true);
            _jumpList = new Mock<IJumpList>(MockBehavior.Strict);
            _searchService = VimUtil.CreateSearchService(_globalSettings.Object);
            _statusUtil = new Mock<IStatusUtil>(MockBehavior.Strict);
            _outlining = new Mock<IOutliningManager>(MockBehavior.Strict);
            _undoRedoOperations = new Mock<IUndoRedoOperations>(MockBehavior.Strict);
            _undoRedoOperations.Setup(x => x.CreateUndoTransaction(It.IsAny<string>())).Returns((new Mock<IUndoTransaction>(MockBehavior.Loose)).Object);
            _registerMap = MockObjectFactory.CreateRegisterMap();

            var data = new OperationsData(
                vimData: _vimData,
                vimHost: _host.Object,
                textView: _textView,
                editorOperations: editorOpts,
                outliningManager: FSharpOption.Create(_outlining.Object),
                statusUtil: _statusUtil.Object,
                jumpList: _jumpList.Object,
                localSettings: _settings.Object,
                registerMap: _registerMap.Object,
                keyMap: null,
                undoRedoOperations: _undoRedoOperations.Object,
                editorOptions: _options.Object,
                navigator: null,
                foldManager: null,
                searchService: _searchService);

            _operationsRaw = new DefaultOperations(data);
            _operations = _operationsRaw;
        }

        private void AllowOutlineExpansion(bool verify = false)
        {
            var res =
                _outlining
                    .Setup(x => x.ExpandAll(It.IsAny<SnapshotSpan>(), It.IsAny<Predicate<ICollapsed>>()))
                    .Returns<IEnumerable<ICollapsible>>(null);
            if (verify)
            {
                res.Verifiable();
            }
        }



        [Test]
        public void JumpNext1()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MoveNext()).Callback(() => { count++; }).Returns(false);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void JumpNext2()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MoveNext()).Callback(() => { count++; }).Returns(() => count == 1);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(2);
            _host.Verify();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void JumpNext3()
        {
            Create("foo", "bar");
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption<SnapshotPoint>.None);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
        }

        [Test]
        public void JumpNext4()
        {
            Create("foo", "bar");
            AllowOutlineExpansion(verify: true);
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_textView.TextSnapshot, 1)));
            _operations.JumpNext(1);
            _host.Verify();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
            _outlining.Verify();
        }

        [Test]
        public void JumpNext5()
        {
            Create("foo", "bar");
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            _jumpList.Setup(x => x.MoveNext()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(point));
            _host.Setup(x => x.NavigateTo(It.IsAny<VirtualSnapshotPoint>())).Returns(true).Verifiable();
            _operations.JumpNext(1);
            _host.Verify();
        }

        [Test]
        public void JumpPrevious1()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MovePrevious()).Callback(() => { count++; }).Returns(false);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void JumpPrevious2()
        {
            Create("foo", "bar");
            var count = 0;
            _jumpList.Setup(x => x.MovePrevious()).Callback(() => { count++; }).Returns(() => count == 1);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(2);
            _host.Verify();
            Assert.AreEqual(2, count);
        }

        [Test]
        public void JumpPrevious3()
        {
            Create("foo", "bar");
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption<SnapshotPoint>.None);
            _host.Setup(x => x.Beep()).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
        }

        [Test]
        public void JumpPrevious4()
        {
            Create("foo", "bar");
            AllowOutlineExpansion(verify: true);
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(new SnapshotPoint(_textView.TextSnapshot, 1)));
            _operations.JumpPrevious(1);
            _host.Verify();
            Assert.AreEqual(1, _textView.GetCaretPoint().Position);
            _outlining.Verify();
        }

        [Test]
        public void JumpPrevious5()
        {
            Create("foo", "bar");
            var point = MockObjectFactory.CreateSnapshotPoint(42);
            _jumpList.Setup(x => x.MovePrevious()).Returns(true);
            _jumpList.SetupGet(x => x.Current).Returns(FSharpOption.Create(point));
            _host.Setup(x => x.NavigateTo(It.IsAny<VirtualSnapshotPoint>())).Returns(true).Verifiable();
            _operations.JumpPrevious(1);
            _host.Verify();
        }

        [Test]
        public void GoToLineOrFirst1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption.Create(1));
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption.Create(42));
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrFirst3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrFirst(FSharpOption<int>.None);
            Assert.AreEqual(0, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test, Description("0 goes to the last line surprisingly and not the first")]
        public void GoToLineOrLast1()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(0));
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast2()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption.Create(1));
            Assert.AreEqual(1, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void GoToLineOrLast3()
        {
            Create("foo", "bar", "baz");
            _globalSettings.SetupGet(x => x.StartOfLine).Returns(true).Verifiable();
            _operations.GoToLineOrLast(FSharpOption<int>.None);
            Assert.AreEqual(2, _textView.GetCaretLine().LineNumber);
            _globalSettings.Verify();
        }

        [Test]
        public void InsertText1()
        {
            Create("foo");
            _operations.InsertText("a", 1);
            Assert.AreEqual("afoo", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText2()
        {
            Create("bar");
            _operations.InsertText("a", 3);
            Assert.AreEqual("aaabar", _textView.TextSnapshot.GetText());
        }

        [Test]
        public void InsertText3()
        {
            Create("bar");
            _textView.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual("bheyar", _textView.TextSnapshot.GetText());
        }

        [Test]
        [Description("Caret needs to be moved to the last letter of the insert")]
        public void InsertText4()
        {
            Create("bar");
            _textView.MoveCaretTo(1);
            _operations.InsertText("hey", 1);
            Assert.AreEqual(3, _textView.GetCaretPoint().Position);
        }
    }
}
