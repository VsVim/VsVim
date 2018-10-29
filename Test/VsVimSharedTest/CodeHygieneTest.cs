using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Vim.UI.Wpf;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    /// <summary>
    /// Pedantic code hygiene tests for the code base
    /// </summary>
    public sealed class CodeHygieneTest
    {
        private readonly Assembly _assembly = typeof(CodeHygieneTest).Assembly;

        private static Assembly[] GetCoreAssemblyList()
        {
            return new[]
            {
                typeof(IVimHost).Assembly,
                typeof(VimHost).Assembly,
                typeof(VsVimHost).Assembly,
                typeof(VsVimPackage).Assembly,
                typeof(ISharedService).Assembly,
            };
        }

        [Fact]
        public void TestNamespace()
        {
            const string prefix = "Vim.VisualStudio.UnitTest.";
            foreach (var type in _assembly.GetTypes().Where(x => x.IsPublic))
            {
                Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal), $"Wrong namespace prefix on {type.FullName}");
            }
        }

        [Fact]
        public void CodeNamespace()
        {
            const string prefix = "Vim.VisualStudio.";
            var assemblies = new[]
                {
                    typeof(ISharedService).Assembly,
                    typeof(IVsAdapter).Assembly
                };
            foreach (var assembly in assemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.FullName.StartsWith("Xaml", StringComparison.Ordinal) ||
                        type.FullName.StartsWith("Microsoft.CodeAnalysis.EmbeddedAttribute", StringComparison.Ordinal) ||
                        type.FullName.StartsWith("System.Runtime.CompilerServices.IsReadOnlyAttribute", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.True(type.FullName.StartsWith(prefix, StringComparison.Ordinal), $"Wrong namespace prefix on {type.FullName}");
                }
            }
        }

        /// <summary>
        /// There should be no references to FSharp.Core in the projects. This should be embedded into 
        /// the Vim.Core assembly and not an actual reference. Too many ways that VS ships the DLL that
        /// it makes referencing it too difficult. Embedding is much more reliably.
        /// </summary>
        [Fact]
        public void FSharpCoreReferences()
        {
            var assemblyList = GetCoreAssemblyList();

            foreach (var assembly in assemblyList)
            {
                foreach (var assemblyRef in assembly.GetReferencedAssemblies())
                {
                    Assert.NotEqual("FSharp.Core", assemblyRef.Name, StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        /// <summary>
        /// Make sure the core assemblies all have the same version and that it matches the release 
        /// version.
        /// </summary>
        [Fact]
        public void AssemblyVersion()
        {
            var assemblyList = GetCoreAssemblyList();
            var version = Version.Parse(VimConstants.VersionNumber);
            foreach (var assembly in assemblyList)
            {
                var assemblyVersion = assembly.GetName().Version;
                Assert.Equal(version, assemblyVersion);
            }
        }
    }
}
