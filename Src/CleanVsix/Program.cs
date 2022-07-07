using Microsoft.VisualStudio.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CleanVsix
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Need a path to the VSIX");
                return;
            }

            try
            {
                var vsixPath = args[0];
                var tempFolderPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Directory.CreateDirectory(tempFolderPath);
                UnzipTo(vsixPath, tempFolderPath);
                CleanFiles(tempFolderPath);
                ZipTo(tempFolderPath, vsixPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void UnzipTo(string vsixPath, string path)
        {
            var decompressor = new ZipFileDecompressor(vsixPath);
            decompressor.UncompressToFolder(path);
            decompressor.Close();
        }

        static void CleanFiles(string path)
        {
            foreach (var filePath in Directory.GetFiles(path))
            {
                var fileName = Path.GetFileName(filePath);
                var delete = false;
                switch (fileName.ToLower())
                {
                    case "system.xml.dll":
                    case "fsharp.core.xml":
                    case "newtonsoft.json.dll":
                    case "envdte.dll":
                    case "envdte80.dll":
                    case "stdole.dll":
                    case "system.collections.immutable.dll":
                    case "system.threading.tasks.extensions.dll":
                    case "microsoft.codeanalysis.scripting.dll":
                    case "microsoft.codeanalysis.dll":
                    case "microsoft.codeanalysis.csharp.scripting.dll":
                    case "microsoft.codeanalysis.csharp.dll":
                        delete = true;
                        break;
                    default:
                        delete = fileName.StartsWith("Microsoft.VisualStudio");
                        break;
                }

                if (delete)
                {
                    File.Delete(filePath);
                }
            }
        }

        static void ZipTo(string sourcePath, string destFilePath)
        {
            // Need to provide relative paths here, not full paths.  
            var files = Directory.GetFiles(sourcePath)
                .Select(x => Path.GetFileName(x))
                .ToArray();
            var z = new ZipFileCompressor(destFilePath, sourcePath, files, deleteIfOutputExists: true);
        }
    }
}
