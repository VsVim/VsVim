using System;
using NUnit.Framework;

namespace Vim.UnitTest
{
    [TestFixture]
    public sealed class SystemUtilTest
    {
        private string _savedHome;
        private string _savedHomeDrive;
        private string _savedHomePath;

        [SetUp]
        public void Setup()
        {
            _savedHome = Environment.GetEnvironmentVariable("HOME");
            _savedHomeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            _savedHomePath = Environment.GetEnvironmentVariable("HOMEPATH");
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("HOME", _savedHome);
            Environment.SetEnvironmentVariable("HOMEDRIVE", _savedHomeDrive);
            Environment.SetEnvironmentVariable("HOMEPATH", _savedHomePath);
        }

        /// <summary>
        /// %HOME% should win here
        /// </summary>
        [Test]
        public void GetHome_PreferHome()
        {
            Environment.SetEnvironmentVariable("HOME", @"c:\foo");
            Assert.AreEqual(@"c:\foo", SystemUtil.GetHome());
        }

        /// <summary>
        /// If %HOME% is not present then it should prefer %HOMEDRIVE%%HOMEPATH%
        /// </summary>
        [Test]
        public void GetHome_ThenPreferHomePathAndDrive()
        {
            Environment.SetEnvironmentVariable("HOME", null);
            Environment.SetEnvironmentVariable("HOMEDRIVE", @"e:\");
            Environment.SetEnvironmentVariable("HOMEPATH", @"bar");
            Assert.AreEqual(@"e:\bar", SystemUtil.GetHome());
        }

        /// <summary>
        /// Don't resolve paths that don't start with a ~
        /// </summary>
        [Test]
        public void ResolvePath_None()
        {
            Assert.AreEqual(@"c:\foo", SystemUtil.ResolvePath(@"c:\foo"));
        }

        [Test]
        public void ResolvePath_Simple()
        {
            Environment.SetEnvironmentVariable("HOME", @"c:\foo");
            Assert.AreEqual(@"c:\foo\bar", SystemUtil.ResolvePath(@"~\bar"));
        }

        /// <summary>
        /// Test the cases we expect to work for CombinePath
        /// </summary>
        [Test]
        public void CombinePath()
        {
            Assert.AreEqual(@"c:\foo", SystemUtil.CombinePath(@"c:", @"\foo"));
            Assert.AreEqual(@"c:\foo", SystemUtil.CombinePath(@"c:\", @"\foo"));
            Assert.AreEqual(@"c:\foo", SystemUtil.CombinePath(@"c:\", @"foo"));
        }

    }
}
