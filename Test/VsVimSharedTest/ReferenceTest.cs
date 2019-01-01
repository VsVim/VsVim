using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Vim.UI.Wpf;
using Xunit;

namespace Vim.VisualStudio.UnitTest
{
    public sealed class ReferenceTest
    {
        internal static Version VisualStudioMajor = new Version(major: 14, minor: 0, build: 0, revision: 0);

        private static IEnumerable<Assembly> GetAssemblies()
        {
            yield return typeof(IVim).Assembly;
            yield return typeof(VimHost).Assembly;
            yield return typeof(VsVimHost).Assembly;
            yield return typeof(ISharedService).Assembly;
        }

        /// <summary>
        /// Make sure the correct VS SDK binaries are referenced.
        /// </summary>
        [Fact]
        public void EnsureCorrectVisualStudioVersion()
        {
            var count = 0;
            var skipped = new HashSet<AssemblyName>();
            foreach (var assembly in GetAssemblies())
            {
                foreach (var n in assembly.GetReferencedAssemblies())
                {
                    var version = getVersion(n);
                    if (version != null)
                    {
                        Assert.Equal(version, n.Version);
                        count++;
                    }
                    else
                    {
                        skipped.Add(n);
                    }
                }
            }

            Assert.True(count >= 19);

            Version getVersion(AssemblyName assemblyName)
            {
                var name = assemblyName.Name;
                if (!name.StartsWith("Microsoft.VisualStudio"))
                {
                    return null;
                }

                // Interop DLLS don't version hence there is only one possible version.
                if (name.Contains("Interop"))
                {
                    return null;
                }

                // Immutable DLLS don't version hence there is only one possible version.
                if (name.Contains("Immutable"))
                {
                    return null;
                }

                // Shell DLLS don't version hence there is only one possible version.
                if (name.Contains("Microsoft.VisualStudio.Shell."))
                {
                    return null;
                }

                return VisualStudioMajor;
            }
        }

        /// <summary>
        /// Make sure that in the future types don't move around that cause us to stop
        /// tracking an assembly.
        /// </summary>
        [Fact]
        public void EnsureCount()
        {
            var set = new HashSet<string>(GetAssemblies().Select(x => x.FullName));
            Assert.Equal(4, set.Count);
        }
    }
}
