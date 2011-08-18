using System;
using System.Linq;
using Microsoft.FSharp.Core;
using NUnit.Framework;
using Vim.UI.Wpf;
using Vim.UI.Wpf.Implementation;
using System.ComponentModel.Composition.Hosting;

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
        private CompositionContainer _compositionContainer;
        private IVim _vim;
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

        protected IVim Vim
        {
            get { return _vim; }
        }

        protected CompositionContainer CompositionContainer
        {
            get { return _compositionContainer; }
        }

        [SetUp]
        public void SetupBase()
        {
            _compositionContainer = GetOrCreateCompositionContainer();
            _vim = _compositionContainer.GetExport<IVim>().Value;
            _vimErrorDetector = _compositionContainer.GetExport<IVimErrorDetector>().Value;
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

            _vim.VimData.LastCommand = FSharpOption<StoredCommand>.None;
            _vim.KeyMap.ClearAll();
            _vim.CloseAllVimBuffers();
        }

        protected virtual CompositionContainer GetOrCreateCompositionContainer()
        {
            return EditorUtil.CompositionContainer;
        }
    }
}
