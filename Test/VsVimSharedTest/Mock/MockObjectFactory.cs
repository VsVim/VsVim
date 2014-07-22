using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using Moq;

namespace Vim.VisualStudio.UnitTest.Mock
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

        public static Mock<EnvDTE.Command> CreateCommand(int id, string name, params string[] bindings)
        {
            return CreateCommand(Guid.NewGuid(), id, name, bindings);
        }

        public static Mock<EnvDTE.Command> CreateCommand(Guid guid, int id, string name, params string[] bindings)
        {
            object binding = bindings.Length == 1
                ? (object)bindings[0]
                : bindings;
            var mock = new Mock<EnvDTE.Command>(MockBehavior.Strict);
            mock.SetupProperty(x => x.Bindings, binding);
            mock.Setup(x => x.Name).Returns(name);
            mock.Setup(x => x.LocalizedName).Returns(name);
            mock.Setup(x => x.Guid).Returns(guid.ToString());
            mock.Setup(x => x.ID).Returns(id);
            return mock;
        }

        public static List<Mock<EnvDTE.Command>> CreateCommandList(params string[] args)
        {
            var list = new List<Mock<EnvDTE.Command>>();
            var count = 0;
            foreach (var binding in args)
            {
                var mock = CreateCommand(++count, "example command", binding);
                list.Add(mock);
            }

            return list;
        }

        public static Mock<EnvDTE.Commands> CreateCommands(List<EnvDTE.Command> commands)
        {
            var mock = new Mock<EnvDTE.Commands>(MockBehavior.Strict);
            var enumMock = mock.As<IEnumerable>();
            mock.Setup(x => x.GetEnumerator()).Returns(() =>
                {
                    return commands.GetEnumerator();
                });
            mock.SetupGet(x => x.Count).Returns(commands.Count);
            enumMock.Setup(x => x.GetEnumerator()).Returns(() =>
                {
                    return commands.GetEnumerator();
                });
            return mock;
        }

        public static Mock<_DTE> CreateDteWithCommands(IEnumerable<EnvDTE.Command> col = null)
        {
            col = col ?? new EnvDTE.Command[] { };
            var commands = CreateCommands(col.ToList());
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
