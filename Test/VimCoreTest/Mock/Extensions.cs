using System;
using System.Linq;
using System.Windows;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Moq.Language.Flow;
using Vim.Extensions;
using EditorUtils;

namespace Vim.UnitTest.Mock
{
    public static class VsVimTestExtensions
    {
        public static Mock<FrameworkElement> MakeVisualElement(
            this Mock<IWpfTextView> textView,
            MockRepository factory)
        {
            factory = factory ?? new MockRepository(MockBehavior.Loose);
            var element = factory.Create<FrameworkElement>();
            textView.SetupGet(x => x.VisualElement).Returns(element.Object);
            return element;
        }

        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            VirtualSnapshotSpan span)
        {
            selection.Setup(x => x.Mode).Returns(TextSelectionMode.Stream);
            selection.Setup(x => x.StreamSelectionSpan).Returns(span);
        }

        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            NormalizedSnapshotSpanCollection col)
        {
            selection.Setup(x => x.Mode).Returns(TextSelectionMode.Box);
            selection.Setup(x => x.SelectedSpans).Returns(col);
            var start = col.Min(x => x.Start);
            var end = col.Min(x => x.End);
            selection
                .Setup(x => x.StreamSelectionSpan)
                .Returns(new VirtualSnapshotSpan(new SnapshotSpan(start, end)));
        }


        public static void MakeSelection(
            this Mock<ITextSelection> selection,
            params SnapshotSpan[] spans)
        {
            if (spans.Length == 1)
            {
                MakeSelection(selection, new VirtualSnapshotSpan(spans[0]));
            }
            else
            {
                MakeSelection(selection, new NormalizedSnapshotSpanCollection(spans));
            }
        }

        public static void MakeUndoRedoPossible(
            this Mock<IUndoRedoOperations> mock,
            MockRepository factory)
        {
            mock
                .Setup(x => x.CreateUndoTransaction(It.IsAny<string>()))
                .Returns(() => factory.Create<IUndoTransaction>(MockBehavior.Loose).Object);
        }

        public static Mock<IEditorOptions> MakeOptions(
            this Mock<IEditorOptionsFactoryService> optionsFactory,
            ITextBuffer buffer,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var options = factory.Create<IEditorOptions>();
            optionsFactory
                .Setup(x => x.GetOptions(buffer))
                .Returns(options.Object);
            return options;
        }

        public static Mock<T> MakeService<T>(
            this Mock<System.IServiceProvider> serviceProvider,
            MockRepository factory = null) where T : class
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var service = factory.Create<T>();
            serviceProvider.Setup(x => x.GetService(typeof(T))).Returns(service.Object);
            return service;
        }

        public static void SetupCommandNormal(this Mock<ICommandUtil> commandUtil, NormalCommand normalCommand, int? count = null, RegisterName registerName = null)
        {
            var realCount = FSharpOption.CreateForNullable(count);
            var realName = FSharpOption.CreateForReference(registerName);
            var commandData = new CommandData(realCount, realName);
            var command = Command.NewNormalCommand(normalCommand, commandData);
            commandUtil
                .Setup(x => x.RunCommand(command))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch))
                .Verifiable();
        }

        public static void SetupCommandMotion<T>(this Mock<ICommandUtil> commandUtil, int? count = null, RegisterName registerName = null) where T : NormalCommand
        {
            var realCount = FSharpOption.CreateForNullable(count);
            var realName = FSharpOption.CreateForReference(registerName);
            var commandData = new CommandData(realCount, realName);
            commandUtil
                .Setup(x => x.RunCommand(It.Is<Command>(c =>
                    c.IsNormalCommand &&
                    c.AsNormalCommand().Item1 is T)))
                .Returns(CommandResult.NewCompleted(ModeSwitch.NoSwitch))
                .Verifiable();
        }

        public static void SetupCommandVisual(this Mock<ICommandUtil> commandUtil, VisualCommand visualCommand, int? count = null, RegisterName registerName = null, VisualSpan visualSpan = null)
        {
            var realCount = count.HasValue ? FSharpOption.Create(count.Value) : FSharpOption<int>.None;
            var realName = registerName != null ? FSharpOption.Create(registerName) : FSharpOption<RegisterName>.None;
            var commandData = new CommandData(realCount, realName);
            if (visualSpan != null)
            {
                var command = Command.NewVisualCommand(visualCommand, commandData, visualSpan);
                commandUtil
                    .Setup(x => x.RunCommand(command))
                    .Returns(CommandResult.NewCompleted(ModeSwitch.SwitchPreviousMode))
                    .Verifiable();
            }
            else
            {
                commandUtil
                    .Setup(x => x.RunCommand(It.Is<Command>(command =>
                            command.IsVisualCommand &&
                            command.AsVisualCommand().Item1.Equals(visualCommand) &&
                            command.AsVisualCommand().Item2.Equals(commandData))))
                    .Returns(CommandResult.NewCompleted(ModeSwitch.SwitchPreviousMode))
                    .Verifiable();
            }

        }

        public static void SetupPut(this Mock<ICommonOperations> operations, ITextBuffer textBuffer, params string[] newText)
        {
            operations
                .Setup(x => x.Put(It.IsAny<SnapshotPoint>(), It.IsAny<StringData>(), It.IsAny<OperationKind>()))
                .Callback(() => textBuffer.SetText(newText))
                .Verifiable();
        }

        public static void SetupProcess(this Mock<INormalMode> mode, string input)
        {
            var count = 0;
            for (var i = 0; i < input.Length; i++)
            {
                var local = i;
                var keyInput = KeyInputUtil.CharToKeyInput(input[i]);
                mode
                    .Setup(x => x.Process(keyInput))
                    .Callback(
                        () =>
                        {
                            if (count != local)
                            {
                                throw new Exception("Wrong order");
                            }
                            count++;
                        })
                    .Returns(ProcessResult.NewHandled(ModeSwitch.NoSwitch));
            }

        }
    }
}
