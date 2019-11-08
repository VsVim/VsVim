using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MonoDevelop.Ide;

namespace Vim.Mac
{
    internal static class ShellWildcardExpansion
    {
        /// <summary>
        /// Expands a wildcard such as **/*.cs into a list of files and
        /// translates paths such as ~/myfile.cs or ../file.cs into the full expanded form
        /// </summary>
        public static IEnumerable<string> ExpandWildcard(string wildcard, string workingDirectory, bool enumerateDirectories = false)
        {
            var args = $"for f in $~vimwildcard; do echo $f; done;";
            var proc = new Process();
            proc.StartInfo.FileName = "zsh";
            proc.StartInfo.Arguments = "-c " + args;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Environment.Add("vimwildcard", wildcard);
            proc.StartInfo.WorkingDirectory = workingDirectory;
            proc.Start();

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            return output.Split('\n')
                         .SelectMany(f => GetFiles(f, enumerateDirectories))
                         .Where(f => IdeServices.DesktopService.GetFileIsText(f));
        }

        static IEnumerable<string> GetFiles(string directoryOrFile,  bool enumerateDirectories)
        {
            if (enumerateDirectories && Directory.Exists(directoryOrFile))
            {
                foreach (var file in Directory.EnumerateFiles(directoryOrFile, "*.*", SearchOption.AllDirectories))
                    yield return file;
            }
            else
            {
                yield return directoryOrFile;
            }
        }
    }
}
