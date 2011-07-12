using System;
using System.Linq;
using NUnit.Framework;
using Vim.UI.Wpf;
using Vim.UI.Wpf.Implementation;

namespace Vim.UnitTest
{
    /// <summary>
    /// Standard test base for vim services which wish to do standard error monitoring like
    ///   - No dangling transactions
    ///   - No silent swallowed MEF errors
    ///   - Remove any key mappings 
    /// </summary>
    [TestFixture]
    public abstract class VimTestBase
    {
        private IVimErrorDetector _vimErrorDetector;
        private IProtectedOperations _protectedOperations;

        /// <summary>
        /// An IProtectedOperations value which will be properly checked in the context of this
        /// test case
        /// </summary>
        protected IProtectedOperations ProtectedOperations
        {
            get { return _protectedOperations; }
        }

        [SetUp]
        public void SetupBase()
        {
            _vimErrorDetector = EditorUtil.FactoryService.VimErrorDetector;
            _vimErrorDetector.Clear();
            _protectedOperations = new ProtectedOperations(_vimErrorDetector);
        }

        [TearDown]
        public void TearDownBase()
        {
            if (_vimErrorDetector.HasErrors())
            {
                var msg = String.Format("Extension Exception: {0}", _vimErrorDetector.GetErrors().First().Message);
                Assert.Fail(msg);
            }

            EditorUtil.FactoryService.Vim.KeyMap.ClearAll();
        }
    }
}
