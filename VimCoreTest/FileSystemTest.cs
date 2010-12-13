using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;

namespace VimCore.UnitTest
{
    [TestFixture]
    public class FileSystemTest
    {
        private Dictionary<string, string> _savedEnvVariables;
        private FileSystem _fileSystemRaw;
        private IFileSystem _fileSystem;

        [SetUp]
        public void SetUp()
        {
            _fileSystemRaw = new FileSystem();
            _fileSystem = _fileSystemRaw;
            _savedEnvVariables = new Dictionary<string,string>();
            foreach ( var name in _fileSystem.EnvironmentVariables )
            {
                _savedEnvVariables[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var pair in _savedEnvVariables)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        [Test]
        public void GetVimRcDirectories1()
        {
            Assert.AreEqual(0, _fileSystem.GetVimRcDirectories().Count());
        }

        [Test]
        public void GetVimRcDirectories2()
        {
            Environment.SetEnvironmentVariable("HOME", @"c:\temp");
            Assert.AreEqual(@"c:\temp", _fileSystem.GetVimRcDirectories().Single());
        }

        [Test]
        public void GetVimRcFilePaths1()
        {
            Environment.SetEnvironmentVariable("HOME", @"c:\temp");
            var list = _fileSystem.GetVimRcFilePaths().ToList();
            Assert.AreEqual(@"c:\temp\.vsvimrc", list[0]);
            Assert.AreEqual(@"c:\temp\.vimrc", list[1]);
            Assert.AreEqual(@"c:\temp\_vimrc", list[2]);
        }

    }
}
