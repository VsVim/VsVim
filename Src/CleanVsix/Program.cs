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
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var delete = false;
                switch (fileName.ToLower())
                {
                    case "fsharp.core":
                    case "system.xml":
                    case "newtonsoft.json":
                    case "envdte":
                    case "envdte80":
                    case "stdole":
                    case "system.collections.immutable":
                    case "system.threading.tasks.extensions":
                    case "microsoft.codeanalysis.scripting":
                    case "microsoft.codeanalysis":
                    case "microsoft.codeanalysis.csharp.scripting":
                    case "microsoft.codeanalysis.csharp":
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
