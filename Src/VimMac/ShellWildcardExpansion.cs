using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide;

namespace Vim.Mac
{
    internal static class ShellWildcardExpansion
    {
        public static IEnumerable<string> ExpandWildcard(string wildcard, string workingDirectory)
        {
            var args = $"for f in $~vimwildcard; do echo $f; done;";
            var proc = new Process();
            proc.StartInfo.FileName = "zsh";
            proc.StartInfo.Arguments = "-c " + EscapeAndQuote(args);
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Environment.Add("vimwildcard", wildcard);
            proc.StartInfo.WorkingDirectory = workingDirectory;
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().TrimEnd('\n');
            proc.WaitForExit();

            var files = output.Split('\n')
                        .SelectMany(GetFiles)
                        .Where(f => IdeServices.DesktopService.GetFileIsText(f));
            return files;
        }

        static IEnumerable<string> GetFiles(string directoryOrFile)
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

        static string EscapeAndQuote(string s)
        {
            var argBuilder = new ProcessArgumentBuilder();
            argBuilder.AddQuoted(s);
            return argBuilder.ToString();
        }
    }
}
