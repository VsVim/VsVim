using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.FindInFiles;
using Provider = MonoDevelop.Ide.FindInFiles.FileProvider;

namespace Vim.Mac
{
    internal class ShellWildcardSearchScope : Scope
    {
        private ImmutableArray<Provider> files;

        public ShellWildcardSearchScope(string workingDirectory, string wildcard)
        {
            files = ShellWildcardExpansion.ExpandWildcard(wildcard, workingDirectory, enumerateDirectories: true)
                        .Select(f => new Provider(f))
                        .ToImmutableArray();
        }

        public override string GetDescription(FilterOptions filterOptions, string pattern, string replacePattern)
        {
            return "Vim wildcard search scope";
        }

        public override IEnumerable<Provider> GetFiles(ProgressMonitor monitor, FilterOptions filterOptions)
        {
            return files;
        }

        public override int GetTotalWork(FilterOptions filterOptions)
        {
            return files.Length; 
        }

    }
}
