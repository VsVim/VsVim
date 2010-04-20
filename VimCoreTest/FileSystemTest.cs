using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Vim;

namespace VimCoreTest
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
    }
}
