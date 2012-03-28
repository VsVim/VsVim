using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;

namespace VsVim.UnitTest.Mock
{
    public static class MockObjectFactory
    {
        public static Mock<SVsServiceProvider> CreateVsServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            return CreateVsServiceProvider(null, serviceList);
        }

        public static Mock<SVsServiceProvider> CreateVsServiceProvider(MockRepository factory, params Tuple<Type, object>[] serviceList)
        {
            var mock = Vim.UnitTest.Mock.MockObjectFactory.CreateServiceProvider(factory, serviceList);
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

        public static Mock<IVsTextLineMarker> CreateVsTextLineMarker(
            TextSpan span,
            MARKERTYPE type,
            MockRepository factory = null)
        {
            return CreateVsTextLineMarker(span, (int)type, factory);
        }

        public static Mock<IVsTextLineMarker> CreateVsTextLineMarker(
            TextSpan span,
            int type,
            MockRepository factory = null)
        {
            factory = factory ?? new MockRepository(MockBehavior.Strict);
            var mock = factory.Create<IVsTextLineMarker>();
            mock.Setup(x => x.GetType(out type)).Returns(VSConstants.S_OK);
            mock
                .Setup(x => x.GetCurrentSpan(It.IsAny<TextSpan[]>()))
                .Callback<TextSpan[]>(x => { x[0] = span; })
                .Returns(VSConstants.S_OK);
            return mock;
        }
    }
}
