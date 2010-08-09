using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using VsVim;

namespace VsVimTest
{
    internal static class MockObjectFactory
    {
        internal static Mock<IServiceProvider> CreateServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            var mock = new Mock<IServiceProvider>(MockBehavior.Strict);
            foreach (var tuple in serviceList)
            {
                mock.Setup(x => x.GetService(tuple.Item1)).Returns(tuple.Item2);
            }

            return mock;
        }

        internal static Mock<SVsServiceProvider> CreateVsServiceProvider(params Tuple<Type, object>[] serviceList)
        {
            var mock = CreateServiceProvider(serviceList);
            return mock.As<SVsServiceProvider>();
        }

        internal static IEnumerable<Mock<Command>> CreateCommandList(params string[] args)
        {
            foreach (var binding in args)
            {
                var localBinding = binding;
                var mock = new Mock<Command>(MockBehavior.Strict);
                mock.Setup(x => x.Bindings).Returns(localBinding);
                mock.Setup(x => x.Name).Returns("example command");
                mock.Setup(x => x.LocalizedName).Returns("example command");
                yield return mock;
            }
        }

        internal static Mock<Commands> CreateCommands(IEnumerable<Command> commands)
        {
            var mock = new Mock<Commands>(MockBehavior.Strict);
            mock.Setup(x => x.GetEnumerator()).Returns(commands.GetEnumerator());
            return mock;
        }

        internal static Mock<_DTE> CreateDteWithCommands(params string[] args)
        {
            var commandList = CreateCommandList(args).Select(x => x.Object);
            var commands = CreateCommands(commandList);
            var dte = new Mock<_DTE>();
            dte.SetupGet(x => x.Commands).Returns(commands.Object);
            return dte;
        }
    }
}
