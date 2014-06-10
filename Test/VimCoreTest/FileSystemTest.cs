using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Xunit;
using Path = System.IO.Path;

namespace Vim.UnitTest
{
    public abstract class FileSystemTest : IDisposable
    {
        private Dictionary<string, string> _savedEnvVariables;
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
                _tempFilePath = System.IO.Path.GetTempFileName();
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
                var line = "let map = ö";
                var encoding = Encoding.GetEncoding("Latin1");
                var bytes = encoding.GetBytes(line);
                File.WriteAllBytes(_tempFilePath, bytes);
                var lines = _fileSystem.ReadAllLines(_tempFilePath).Value;
                Assert.Equal(line, lines[0]);
            }

            [Fact]
            public void UmlautWithBom()
            {
                var line = "let map = ö";
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
                Assert.Equal(1, _fileSystem.GetVimRcDirectories().Count());
            }

            [Fact]
            public void GetVimRcDirectories2()
            {
                Environment.SetEnvironmentVariable("HOME", @"c:\temp");
                Assert.Equal(@"c:\temp", _fileSystem.GetVimRcDirectories().Single());
            }

            [Fact]
            public void GetVimRcFilePaths1()
            {
                Environment.SetEnvironmentVariable("HOME", @"c:\temp");
                var list = _fileSystemRaw.GetVimRcFilePaths().Select(x => x.FilePath).ToList();
                Assert.Equal(@"c:\temp\.vsvimrc", list[0]);
                Assert.Equal(@"c:\temp\_vsvimrc", list[1]);
                Assert.Equal(@"c:\temp\.vimrc", list[2]);
                Assert.Equal(@"c:\temp\_vimrc", list[3]);
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
                Assert.Equal(@"c:\temp\.vimrc", list[2]);
                Assert.Equal(@"c:\temp\_vimrc", list[3]);
            }
        }
    }
}
