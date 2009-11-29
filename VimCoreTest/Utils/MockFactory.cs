using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VimCore;
using Moq;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest.Utils
{
    internal static class MockFactory
    {
        internal static Mock<IRegisterMap> CreateRegisterMap()
        {
            var mock = new Mock<IRegisterMap>();
            var reg = new Register('_');
            mock.Setup(x => x.DefaultRegisterName).Returns('_');
            mock.Setup(x => x.DefaultRegister).Returns(reg);
            return mock;
        }

        internal static Mock<IVimData> CreateVimData(
            IRegisterMap registerMap = null,
            MarkMap map = null)
        {
            registerMap = registerMap ?? CreateRegisterMap().Object;
            map = map ?? new MarkMap();
            var mock = new Mock<IVimData>(MockBehavior.Strict);
            mock.Setup(x => x.RegisterMap).Returns(registerMap);
            mock.Setup(x => x.MarkMap).Returns(map);
            return mock;
        }

        internal static Mock<IVim> CreateVim(
            IVimData data = null)
        {
            data = data ?? CreateVimData().Object;
            var mock = new Mock<IVim>(MockBehavior.Strict);
            mock.Setup(x => x.Data).Returns(data);
            return mock;
        }

        internal static VimBufferData CreateVimBufferData(
            IWpfTextView view, 
            string name = null,
            IVimHost host = null, 
            IVimData data = null)
        {
            name = name ?? "test";
            host = host ?? new FakeVimHost();
            data = data ?? CreateVimData().Object;
            return new VimBufferData("test", view, host, data);
        }
    }
}
