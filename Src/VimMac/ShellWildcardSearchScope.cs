using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Ide.FindInFiles;

namespace Vim.Mac
{
    internal class ShellWildcardSearchScope : Scope
    {
        private ImmutableArray<FileProvider> files;

        public ShellWildcardSearchScope(string workingDirectory, string wildcard)
        {
            files = ShellWildcardExpansion.ExpandWildcard(wildcard, workingDirectory)
                        .Select(f => new FileProvider(f))
                        .ToImmutableArray();
        }

        public override string GetDescription(FilterOptions filterOptions, string pattern, string replacePattern)
        {
            return "Vim wildcard search scope";
        }

        public override IEnumerable<FileProvider> GetFiles(ProgressMonitor monitor, FilterOptions filterOptions)
        {
            return files;
        }

        public override int GetTotalWork(FilterOptions filterOptions)
        {
            return files.Length; 
        }

    }
}
