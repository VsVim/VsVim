using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Vim.UnitTest
{
    public class FileSystemTest : IDisposable
    {
        private Dictionary<string, string> _savedEnvVariables;
        private FileSystem _fileSystemRaw;
        private IFileSystem _fileSystem;

        public FileSystemTest()
        {
            _fileSystemRaw = new FileSystem();
            _fileSystem = _fileSystemRaw;
            _savedEnvVariables = new Dictionary<string, string>();

            foreach (var name in _fileSystem.EnvironmentVariables.SelectMany(ev => ev.Split(new[] { '%' }, StringSplitOptions.RemoveEmptyEntries)))
            {
                _savedEnvVariables[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        public void Dispose()
        {
            foreach (var pair in _savedEnvVariables)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        [Fact]
        public void GetVimRcDirectories1()
        {
            Assert.Equal(0, _fileSystem.GetVimRcDirectories().Count());
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
            var list = _fileSystem.GetVimRcFilePaths().ToList();
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
            var filePath = _fileSystem.GetVimRcFilePaths().First();
            Assert.Equal(@"c:\temp\.vimrc", filePath);
            Environment.SetEnvironmentVariable("MYVIMRC", null);
        }

        [Fact]
        public void HomeDrivePathTakesPrecedenceOverUserProfile()
        {
            Environment.SetEnvironmentVariable("HOMEDRIVE", "c:");
            Environment.SetEnvironmentVariable("HOMEPATH", "\\temp");
            Environment.SetEnvironmentVariable("USERPROFILE", "c:\\Users");
            var list = _fileSystem.GetVimRcFilePaths().ToList();
            Assert.Equal(@"c:\temp\.vsvimrc", list[0]);
            Assert.Equal(@"c:\temp\_vsvimrc", list[1]);
            Assert.Equal(@"c:\temp\.vimrc", list[2]);
            Assert.Equal(@"c:\temp\_vimrc", list[3]);
        }

    }
}
