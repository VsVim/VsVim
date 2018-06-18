using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Xunit;

namespace Vim.UnitTest
{
    public abstract class FileSystemTest : IDisposable
    {
        private readonly Dictionary<string, string> _savedEnvVariables;
        private FileSystem _fileSystemRaw;
        private IFileSystem _fileSystem;

        public FileSystemTest()
        {
            _fileSystemRaw = new FileSystem();
            _fileSystem = _fileSystemRaw;
            _savedEnvVariables = new Dictionary<string, string>();

            // Clear variables used while processing "~"
            RecordAndClearVariable("HOME");
            RecordAndClearVariable("HOMEDRIVE");
            RecordAndClearVariable("HOMEPATH");

            // Clear variables used while processing candidate directories
            var names = FileSystem.VimRcDirectoryCandidates
                .Where(candidate => candidate.StartsWith("$"))
                .Select(candidate => candidate.Substring(1));
            foreach (var name in names)
            {
                RecordAndClearVariable(name);
            }
        }

        public void RecordAndClearVariable(string name)
        {
            _savedEnvVariables[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, null);
        }

        public virtual void Dispose()
        {
            foreach (var pair in _savedEnvVariables)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        public sealed class EncodingTest : FileSystemTest
        {
            private readonly string _tempFilePath;

            public EncodingTest()
            {
                _tempFilePath = Path.GetTempFileName();
            }

            public override void Dispose()
            {
                base.Dispose();
                File.Delete(_tempFilePath);
            }

            /// <summary>
            /// Make sure the encoding detection can handle a file with an umlaut in it that doesn't
            /// have a specific byte order marker.  UTF8 can't handle this correctly and the 
            /// implementation must fall back to a Latin1 encoding
            /// </summary>
            [Fact]
            public void UmlautNoBom()
            {
                var line = "let map = \u00F6";
                var encoding = Encoding.GetEncoding("Latin1");
                var bytes = encoding.GetBytes(line);
                File.WriteAllBytes(_tempFilePath, bytes);
                var lines = _fileSystem.ReadAllLines(_tempFilePath).Value;
                Assert.Equal(line, lines[0]);
            }

            [Fact]
            public void UmlautWithBom()
            {
                var line = "let map = \u00F6";
                var encoding = Encoding.GetEncoding("Latin1");
                File.WriteAllLines(_tempFilePath, new[] { line }, encoding);
                var lines = _fileSystem.ReadAllLines(_tempFilePath).Value;
                Assert.Equal(line, lines[0]);
            }
        }

        public sealed class MiscTest : FileSystemTest
        {
            [Fact]
            public void GetVimRcDirectories1()
            {
                // "~" is always valid, even if HOME, etc. are undefined.
                // "~\\vimfiles" is always valid, even if HOME, etc. are undefined.
                Assert.Equal(2, _fileSystem.GetVimRcDirectories().Count());
            }

            [Fact]
            public void GetVimRcDirectories2()
            {
                Environment.SetEnvironmentVariable("HOME", @"c:\temp");
                var list = _fileSystem.GetVimRcDirectories().ToList();
                Assert.Equal(@"c:\temp", list[0]);
                Assert.Equal(@"c:\temp\vimfiles", list[1]);
            }

            [Fact]
            public void GetVimRcFilePaths1()
            {
                Environment.SetEnvironmentVariable("HOME", @"c:\temp");
                var list = _fileSystemRaw.GetVimRcFilePaths().Select(x => x.FilePath).ToList();
                Assert.Equal(@"c:\temp\.vsvimrc", list[0]);
                Assert.Equal(@"c:\temp\_vsvimrc", list[1]);
                Assert.Equal(@"c:\temp\vsvimrc", list[2]);
                Assert.Equal(@"c:\temp\.vimrc", list[3]);
                Assert.Equal(@"c:\temp\_vimrc", list[4]);
                Assert.Equal(@"c:\temp\vimrc", list[5]);
                Assert.Equal(@"c:\temp\vimfiles\.vsvimrc", list[6]);
                Assert.Equal(@"c:\temp\vimfiles\_vsvimrc", list[7]);
                Assert.Equal(@"c:\temp\vimfiles\vsvimrc", list[8]);
                Assert.Equal(@"c:\temp\vimfiles\.vimrc", list[9]);
                Assert.Equal(@"c:\temp\vimfiles\_vimrc", list[10]);
                Assert.Equal(@"c:\temp\vimfiles\vimrc", list[11]);
                Assert.Equal(12, list.Count);
            }

            /// <summary>
            /// If the MYVIMRC environment variable is set then prefer that over the standard
            /// paths
            /// </summary>
            [Fact]
            public void GetVimRcFilePaths_MyVimRc()
            {
                Environment.SetEnvironmentVariable("MYVIMRC", @"c:\temp\.vimrc");
                var filePath = _fileSystemRaw.GetVimRcFilePaths().Select(x => x.FilePath).First();
                Assert.Equal(@"c:\temp\.vimrc", filePath);
                Environment.SetEnvironmentVariable("MYVIMRC", null);
            }

            [Fact]
            public void HomeDrivePathTakesPrecedenceOverUserProfile()
            {
                Environment.SetEnvironmentVariable("HOMEDRIVE", "c:");
                Environment.SetEnvironmentVariable("HOMEPATH", "\\temp");
                Environment.SetEnvironmentVariable("USERPROFILE", "c:\\Users");
                var list = _fileSystemRaw.GetVimRcFilePaths().Select(x => x.FilePath).ToList();
                Assert.Equal(@"c:\temp\.vsvimrc", list[0]);
                Assert.Equal(@"c:\temp\_vsvimrc", list[1]);
                Assert.Equal(@"c:\temp\vsvimrc", list[2]);
                Assert.Equal(@"c:\temp\.vimrc", list[3]);
                Assert.Equal(@"c:\temp\_vimrc", list[4]);
                Assert.Equal(@"c:\temp\vimrc", list[5]);
            }
        }
    }
}
