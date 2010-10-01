using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Moq;

namespace Vim.UnitTest.Mock
{
    public static class MockObjectFactory
    {
        public static Mock<IRegisterMap> CreateRegisterMap()
        {
            var mock = new Mock<IRegisterMap>();
            var reg = new Register('_');
            mock.Setup(x => x.DefaultRegisterName).Returns('_');
            mock.Setup(x => x.DefaultRegister).Returns(reg);
            return mock;
        }

        public static Mock<ITrackingLineColumnService> CreateTrackingLineColumnService()
        {
            var mock = new Mock<ITrackingLineColumnService>(MockBehavior.Strict);
            return mock;
        }

        public static Mock<IVim> CreateVim(
            IRegisterMap registerMap = null,
            MarkMap map = null,
            IVimGlobalSettings settings = null,
            IVimHost host = null,
            IKeyMap keyMap = null,
            IChangeTracker changeTracker = null,
            IKeyboardDevice keyboardDevice = null,
            IMouseDevice mouseDevice = null)
        {
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap(new TrackingLineColumnService());
            settings = settings ?? new GlobalSettings();
            host = host ?? new MockVimHost();
            keyMap = keyMap ?? (new KeyMap());
            keyboardDevice = keyboardDevice ?? (new Mock<IKeyboardDevice>(MockBehavior.Loose)).Object;
            mouseDevice = mouseDevice ?? (new Mock<IMouseDevice>(MockBehavior.Loose)).Object;
            changeTracker = changeTracker ?? new ChangeTracker(new TextChangeTrackerFactory(keyboardDevice, mouseDevice));
            var mock = new Mock<IVim>(MockBehavior.Strict);
            mock.Setup(x => x.RegisterMap).Returns(registerMap);
            mock.Setup(x => x.MarkMap).Returns(map);
            mock.Setup(x => x.Settings).Returns(settings);
            mock.Setup(x => x.VimHost).Returns(host);
            mock.Setup(x => x.KeyMap).Returns(keyMap);
            mock.Setup(x => x.ChangeTracker).Returns(changeTracker);
            return mock;
        }

        public static Mock<IEditorOperations> CreateEditorOperations()
        {
            var mock = new Mock<IEditorOperations>(MockBehavior.Strict);
            return mock;
        }

        public static Mock<IVimGlobalSettings> CreateGlobalSettings(
            bool? ignoreCase = null,
            int? shiftWidth = null)
        {
            var mock = new Mock<IVimGlobalSettings>(MockBehavior.Strict);
            if (ignoreCase.HasValue)
            {
                mock.SetupGet(x => x.IgnoreCase).Returns(ignoreCase.Value);
            }
            if (shiftWidth.HasValue)
            {
                mock.SetupGet(x => x.ShiftWidth).Returns(shiftWidth.Value);
            }

            mock.SetupGet(x => x.DisableCommand).Returns(GlobalSettings.DisableCommand);
            return mock;
        }

        public static Mock<IVimLocalSettings> CreateLocalSettings(
            IVimGlobalSettings global = null)
        {
            global = global ?? CreateGlobalSettings().Object;
            var mock = new Mock<IVimLocalSettings>(MockBehavior.Strict);
            mock.SetupGet(x => x.GlobalSettings).Returns(global);
            return mock;
        }

        public static Mock<IVimBuffer> CreateVimBuffer(
            ITextView view,
            string name = null,
            IVim vim = null,
            IJumpList jumpList = null,
            IVimLocalSettings settings = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            name = name ?? "test";
            vim = vim ?? CreateVim().Object;
            jumpList = jumpList ?? (factory.Create<IJumpList>().Object);
            settings = settings ?? new LocalSettings(vim.Settings, view);
            var mock = factory.Create<IVimBuffer>();
            mock.SetupGet(x => x.TextView).Returns(view);
            mock.SetupGet(x => x.TextBuffer).Returns(() => view.TextBuffer);
            mock.SetupGet(x => x.TextSnapshot).Returns(() => view.TextSnapshot);
            mock.SetupGet(x => x.Name).Returns(name);
            mock.SetupGet(x => x.Settings).Returns(settings);
            mock.SetupGet(x => x.MarkMap).Returns(vim.MarkMap);
            mock.SetupGet(x => x.RegisterMap).Returns(vim.RegisterMap);
            mock.SetupGet(x => x.JumpList).Returns(jumpList);
            mock.SetupGet(x => x.Vim).Returns(vim);
            return mock;
        }

        public static Mock<ITextCaret> CreateCaret(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<ITextCaret>();
        }

        public static Mock<ITextSelection> CreateSelection(MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            return factory.Create<ITextSelection>();
        }

        public static Mock<ITextView> CreateTextView(
            ITextBuffer buffer = null,
            ITextCaret caret = null,
            ITextSelection selection = null,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            buffer = buffer ?? CreateTextBuffer(100, factory: factory).Object;
            caret = caret ?? CreateCaret(factory: factory).Object;
            selection = selection ?? CreateSelection(factory: factory).Object;
            var view = factory.Create<ITextView>();
            view.SetupGet(x => x.Caret).Returns(caret);
            view.SetupGet(x => x.Selection).Returns(selection);
            view.SetupGet(x => x.TextBuffer).Returns(buffer);
            view.SetupGet(x => x.TextSnapshot).Returns(() => buffer.CurrentSnapshot);
            return view;
        }

        public static Tuple<Mock<ITextView>, Mock<ITextCaret>, Mock<ITextSelection>> CreateTextViewAll(ITextBuffer buffer)
        {
            var caret = CreateCaret();
            var selection = CreateSelection();
            var view = CreateTextView(buffer, caret.Object, selection.Object);
            return Tuple.Create(view, caret, selection);
        }

        public static Tuple<Mock<ITextView>, MockRepository> CreateTextViewWithVisibleLines(
            ITextBuffer buffer,
            int startLine,
            int? endLine = null,
            int? caretPosition = null)
        {
            var factory = new MockRepository(MockBehavior.Strict);
            var endLineValue = endLine ?? startLine;
            var caretPositionValue = caretPosition ?? buffer.GetLine(startLine).Start.Position;
            var caret = factory.Create<ITextCaret>();
            caret.SetupGet(x => x.Position).Returns(
                new CaretPosition(
                    new VirtualSnapshotPoint(buffer.GetPoint(caretPositionValue)),
                    factory.Create<IMappingPoint>().Object,
                    PositionAffinity.Predecessor));

            var firstLine = factory.Create<ITextViewLine>();
            firstLine.SetupGet(x => x.Start).Returns(buffer.GetLine(startLine).Start);

            var lastLine = factory.Create<ITextViewLine>();
            lastLine.SetupGet(x => x.End).Returns(buffer.GetLine(endLineValue).End);

            var lines = factory.Create<ITextViewLineCollection>();
            lines.SetupGet(x => x.FirstVisibleLine).Returns(firstLine.Object);
            lines.SetupGet(x => x.LastVisibleLine).Returns(lastLine.Object);

            var view = factory.Create<ITextView>();
            view.SetupGet(x => x.TextBuffer).Returns(buffer);
            view.SetupGet(x => x.TextViewLines).Returns(lines.Object);
            view.SetupGet(x => x.Caret).Returns(caret.Object);
            view.SetupGet(x => x.InLayout).Returns(false);
            view.SetupGet(x => x.TextSnapshot).Returns(() => buffer.CurrentSnapshot);
            return Tuple.Create(view, factory);
        }

        public static Mock<ITextBuffer> CreateTextBuffer(int? snapshotLength = null, MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<ITextBuffer>();
            mock.SetupGet(x => x.Properties).Returns(new Microsoft.VisualStudio.Utilities.PropertyCollection());
            var snapshot = CreateTextSnapshot(snapshotLength ?? 0, mock.Object);
            mock.SetupGet(x => x.CurrentSnapshot).Returns(snapshot.Object);
            return mock;
        }

        public static Mock<ITextVersion> CreateTextVersion(int? versionNumber = null)
        {
            var number = versionNumber ?? 1;
            var mock = new Mock<ITextVersion>(MockBehavior.Strict);
            mock.SetupGet(x => x.VersionNumber).Returns(number);
            return mock;
        }

        public static Mock<ITextSnapshot> CreateTextSnapshot(
            int length,
            ITextBuffer buffer = null,
            int? versionNumber = null)
        {

            buffer = buffer ?? CreateTextBuffer().Object;
            var mock = new Mock<ITextSnapshot>(MockBehavior.Strict);
            mock.SetupGet(x => x.Length).Returns(length);
            mock.SetupGet(x => x.TextBuffer).Returns(buffer);
            mock.SetupGet(x => x.Version).Returns(CreateTextVersion(versionNumber).Object);
            return mock;
        }

        public static SnapshotPoint CreateSnapshotPoint(int position)
        {
            var snapshot = CreateTextSnapshot(position + 1);
            snapshot.Setup(x => x.GetText(It.IsAny<int>(), It.IsAny<int>())).Returns("Mocked ToString()");
            return new SnapshotPoint(snapshot.Object, position);
        }

        public static Mock<IServiceProvider> CreateServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            var mock = new Mock<IServiceProvider>(MockBehavior.Strict);
            foreach (var tuple in serviceList)
            {
                mock.Setup(x => x.GetService(tuple.Item1)).Returns(tuple.Item2);
            }

            return mock;
        }

        public static Mock<SVsServiceProvider> CreateVsServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            var mock = CreateServiceProvider(serviceList);
            return mock.As<SVsServiceProvider>();
        }

        public static IEnumerable<Mock<EnvDTE.Command>> CreateCommandList(params string[] args)
        {
            foreach (var binding in args)
            {
                var localBinding = binding;
                var mock = new Mock<EnvDTE.Command>(MockBehavior.Strict);
                mock.Setup(x => x.Bindings).Returns(localBinding);
                mock.Setup(x => x.Name).Returns("example command");
                mock.Setup(x => x.LocalizedName).Returns("example command");
                yield return mock;
            }
        }

        public static Mock<EnvDTE.Commands> CreateCommands(IEnumerable<EnvDTE.Command> commands)
        {
            var mock = new Mock<EnvDTE.Commands>(MockBehavior.Strict);
            var enumMock = mock.As<IEnumerable>();
            mock.Setup(x => x.GetEnumerator()).Returns(commands.GetEnumerator());
            enumMock.Setup(x => x.GetEnumerator()).Returns(commands.GetEnumerator());
            return mock;
        }

        public static Mock<_DTE> CreateDteWithCommands(params string[] args)
        {
            var commandList = CreateCommandList(args).Select(x => x.Object);
            var commands = CreateCommands(commandList);
            var dte = new Mock<_DTE>();
            dte.SetupGet(x => x.Commands).Returns(commands.Object);
            return dte;
        }
    }
}
