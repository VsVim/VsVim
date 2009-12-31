using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vim;
using Moq;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace VimCoreTest.Utils
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

        internal static Mock<IVim> CreateVim(
            IRegisterMap registerMap = null,
            MarkMap map = null,
            VimSettings settings = null,
            IVimHost host = null)
        {
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap();
            settings = settings ?? VimSettingsUtil.CreateDefault;
            host = host ?? new FakeVimHost();
            var mock = new Mock<IVim>(MockBehavior.Strict);
            mock.Setup(x => x.RegisterMap).Returns(registerMap);
            mock.Setup(x => x.MarkMap).Returns(map);
            mock.Setup(x => x.Settings).Returns(settings);
            mock.Setup(x => x.Host).Returns(host);
            return mock;
        }

        internal static Mock<IBlockCaret> CreateBlockCaret()
        {
            var mock = new Mock<IBlockCaret>(MockBehavior.Loose);
            return mock;
        }

        internal static Mock<IEditorOperations> CreateEditorOperations()
        {
            var mock = new Mock<IEditorOperations>(MockBehavior.Strict);
            return mock;
        }

        internal static Mock<IVimBuffer> CreateVimBuffer(
            IWpfTextView view, 
            string name = null,
            IVim vim = null,
            IBlockCaret caret = null,
            IEditorOperations editorOperations = null)
        {
            name = name ?? "test";
            vim = vim ?? CreateVim().Object;
            caret = caret ?? CreateBlockCaret().Object;
            editorOperations = editorOperations ?? CreateEditorOperations().Object;
            var mock = new Mock<IVimBuffer>(MockBehavior.Strict);
            mock.SetupGet(x => x.TextView).Returns(view);
            mock.SetupGet(x => x.TextBuffer).Returns(() => view.TextBuffer);
            mock.SetupGet(x => x.TextSnapshot).Returns(() => view.TextSnapshot);
            mock.SetupGet(x => x.Name).Returns(name);
            mock.SetupGet(x => x.BlockCaret).Returns(caret);
            mock.SetupGet(x => x.EditorOperations).Returns(editorOperations);
            mock.SetupGet(x => x.VimHost).Returns(vim.Host);
            mock.SetupGet(x => x.Settings).Returns(vim.Settings);
            mock.SetupGet(x => x.MarkMap).Returns(vim.MarkMap);
            mock.SetupGet(x => x.RegisterMap).Returns(vim.RegisterMap);
            return mock;
        }
    }
}
