using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VimCore;
using Moq;
using Microsoft.VisualStudio.Text.Editor;

namespace VimCoreTest.Utils
{
    public static class MockFactory
    {
        public static Mock<IRegisterMap> CreateRegisterMap()
        {
            var mock = new Mock<IRegisterMap>();
            var reg = new Register('_');
            mock.Setup(x => x.DefaultRegisterName).Returns('_');
            mock.Setup(x => x.DefaultRegister).Returns(reg);
            return mock;
        }

        public static VimBufferData CreateVimBufferData(IWpfTextView view)
        {
            return CreateVimBufferData(view, new FakeVimHost(), CreateRegisterMap().Object);
        }

        public static VimBufferData CreateVimBufferData(IWpfTextView view, IVimHost host, IRegisterMap map)
        {
            return new VimBufferData("test", view, host, map);

        }
    }
}
