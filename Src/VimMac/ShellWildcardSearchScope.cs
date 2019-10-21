using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;
using MonoDevelop.Ide.FindInFiles;

namespace Vim.Mac
{
    public class ShellWildcardSearchScope : Scope
    {
        private ImmutableArray<FileProvider> files;

        public ShellWildcardSearchScope(string workingDirectory, string wildcard)
        {
            var args = "echo " + wildcard;
            var proc = new Process();
            proc.StartInfo.FileName = "zsh";
            proc.StartInfo.Arguments = "-c " + EscapeAndQuote(args);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.WorkingDirectory = workingDirectory;
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().TrimEnd('\n');
            proc.WaitForExit();

            //TODO: handle filenames containing spaces
            files = output.Split(' ')
                        .SelectMany(GetFiles)
                        .Where(f => IdeServices.DesktopService.GetFileIsText(f))
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

        IEnumerable<string> GetFiles(string directoryOrFile)
        {
            if (Directory.Exists(directoryOrFile))
            {
                foreach (var file in Directory.EnumerateFiles(directoryOrFile, "*.*", SearchOption.AllDirectories))
                    yield return file;
            }
            else
            {
                yield return directoryOrFile;
            }
        }

        string EscapeAndQuote(string s)
        {
            var argBuilder = new ProcessArgumentBuilder();
            argBuilder.AddQuoted(s);
            return argBuilder.ToString();
        }
    }
}
