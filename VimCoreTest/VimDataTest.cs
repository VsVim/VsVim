using System;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using NUnit.Framework;
using Vim;
using Vim.Extensions;
using Vim.UnitTest;
using Vim.UnitTest.Mock;

namespace VimCore.UnitTest
{
    [TestFixture]
    public sealed class VimDataTest
    {
        private VimData _vimDataRaw;
        private IVimData _vimData;

        [SetUp]
        public void Setup()
        {
            _vimDataRaw = new VimData();
            _vimData = _vimDataRaw;
        }

        /// <summary>
        /// The startup value for CurrentDirectory should be a non-empty string
        /// </summary>
        [Test]
        public void CurrentDirectory_Initial()
        {
            Assert.IsFalse(String.IsNullOrEmpty(_vimData.CurrentDirectory));
        }

        /// <summary>
        /// Setting the current directory should move the previous value to PreviousCurrentDirectory
        /// </summary>
        [Test]
        public void CurrentDirectory_SetUpdatePrevious()
        {
            var old = _vimData.CurrentDirectory;
            _vimData.CurrentDirectory = @"c:\";
            Assert.AreEqual(old, _vimData.PreviousCurrentDirectory);
        }
    }
}
