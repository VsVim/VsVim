using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text;

namespace VimCore.Test.Mock
{
    internal static class MockObjectFactory
    {
        internal static Mock<IRegisterMap> CreateRegisterMap()
        {
            var mock = new Mock<IRegisterMap>();
            var reg = new Register('_');
            mock.Setup(x => x.DefaultRegisterName).Returns('_');
            mock.Setup(x => x.DefaultRegister).Returns(reg);
            return mock;
        }

        internal static Mock<ITrackingLineColumnService> CreateTrackingLineColumnService()
        {
            var mock = new Mock<ITrackingLineColumnService>(MockBehavior.Strict);
            return mock;
        }

        internal static Mock<IVim> CreateVim(
            IRegisterMap registerMap = null,
            MarkMap map = null,
            IVimGlobalSettings settings = null,
            IVimHost host = null,
            IKeyMap keyMap = null,
            IChangeTracker changeTracker = null)
        {
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap(new TrackingLineColumnService());
            settings = settings ?? new GlobalSettings();
            host = host ?? new FakeVimHost();
            keyMap = keyMap ?? (new KeyMap());
            changeTracker = changeTracker ?? new ChangeTracker();
            var mock = new Mock<IVim>(MockBehavior.Strict);
            mock.Setup(x => x.RegisterMap).Returns(registerMap);
            mock.Setup(x => x.MarkMap).Returns(map);
            mock.Setup(x => x.Settings).Returns(settings);
            mock.Setup(x => x.VimHost).Returns(host);
            mock.Setup(x => x.KeyMap).Returns(keyMap);
            mock.Setup(x => x.ChangeTracker).Returns(changeTracker);
            return mock;
        }

        internal static Mock<IEditorOperations> CreateEditorOperations()
        {
            var mock = new Mock<IEditorOperations>(MockBehavior.Strict);
            return mock;
        }

        internal static Mock<IVimGlobalSettings> CreateGlobalSettings(
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

        internal static Mock<IVimLocalSettings> CreateLocalSettings(
            IVimGlobalSettings global = null)
        {
            global = global ?? CreateGlobalSettings().Object;
            var mock = new Mock<IVimLocalSettings>(MockBehavior.Strict);
            mock.SetupGet(x => x.GlobalSettings).Returns(global);
            return mock;
        }


        internal static Mock<IVimBuffer> CreateVimBuffer(
            ITextView view,
            string name = null,
            IVim vim = null,
            IJumpList jumpList = null,
            IVimLocalSettings settings = null )
        {
            name = name ?? "test";
            vim = vim ?? CreateVim().Object;
            jumpList = jumpList ?? (new Mock<IJumpList>(MockBehavior.Strict)).Object;
            settings = settings ?? new LocalSettings(vim.Settings, view);
            var mock = new Mock<IVimBuffer>(MockBehavior.Strict);
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

        internal static Mock<ITextCaret> CreateCaret()
        {
            return new Mock<ITextCaret>(MockBehavior.Strict);
        }

        internal static Mock<ITextSelection> CreateSelection()
        {
            return new Mock<ITextSelection>(MockBehavior.Strict);
        }

        internal static Mock<ITextView> CreateTextView(
            ITextBuffer buffer = null,
            ITextCaret caret = null,
            ITextSelection selection = null)
        {
            buffer = buffer ?? CreateTextBuffer(addSnapshot:true).Object;
            caret = caret ?? CreateCaret().Object;
            selection = selection ?? CreateSelection().Object;
            var view = new Mock<ITextView>(MockBehavior.Strict);
            view.SetupGet(x => x.Caret).Returns(caret);
            view.SetupGet(x => x.Selection).Returns(selection);
            view.SetupGet(x => x.TextBuffer).Returns(buffer);
            view.SetupGet(x => x.TextSnapshot).Returns(() => buffer.CurrentSnapshot);
            return view;
        }

        internal static Tuple<Mock<ITextView>, Mock<ITextCaret>, Mock<ITextSelection>> CreateTextViewAll(ITextBuffer buffer)
        {
            var caret = CreateCaret();
            var selection = CreateSelection();
            var view = CreateTextView(buffer, caret.Object, selection.Object);
            return Tuple.Create(view, caret, selection);
        }

        internal static Mock<ITextBuffer> CreateTextBuffer(bool addSnapshot=false)
        {
            var mock = new Mock<ITextBuffer>(MockBehavior.Strict);
            mock.SetupGet(x => x.Properties).Returns(new Microsoft.VisualStudio.Utilities.PropertyCollection());
            if (addSnapshot)
            {
                mock.SetupGet(x => x.CurrentSnapshot).Returns(CreateTextSnapshot(42).Object);
            }
            return mock;
        }

        internal static Mock<ITextVersion> CreateTextVersion(int? versionNumber = null)
        {
            var number = versionNumber ?? 1;
            var mock = new Mock<ITextVersion>(MockBehavior.Strict);
            mock.SetupGet(x => x.VersionNumber).Returns(number);
            return mock;
        }

        internal static Mock<ITextSnapshot> CreateTextSnapshot(
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

        internal static SnapshotPoint CreateSnapshotPoint( int position )
        {
            var snapshot = CreateTextSnapshot(position + 1);
            snapshot.Setup(x => x.GetText(It.IsAny<int>(), It.IsAny<int>())).Returns("Mocked ToString()");
            return new SnapshotPoint(snapshot.Object, position);
        }
    }
}
