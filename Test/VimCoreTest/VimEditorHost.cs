using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using EditorUtils;
using Microsoft.VisualStudio.Text.Classification;
using Vim.UI.Wpf;
using Vim.UnitTest.Mock;

namespace Vim.UnitTest
{
    public sealed class VimEditorHost : EditorHost
    {
        private readonly IVim _vim;
        private readonly IVimBufferFactory _vimBufferFactory;
        private readonly ICommonOperationsFactory _commonOperationsFactory;
        private readonly IVimErrorDetector _vimErrorDetector;
        private readonly IWordUtil _wordUtil;
        private readonly IFoldManagerFactory _foldManagerFactory;
        private readonly IBufferTrackingService _bufferTrackingService;
        private readonly IBulkOperations _bulkOperations;
        private readonly IKeyUtil _keyUtil;
        private readonly IKeyboardDevice _keyboardDevice;
        private readonly IMouseDevice _mouseDevice;
        private readonly IClipboardDevice _clipboardDevice;
        private readonly IVimProtectedOperations _vimProtectedOperations;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IClassificationFormatMapService _classificationFormatMapService;

        public IVim Vim
        {
            get { return _vim; }
        }

        public IVimData VimData
        {
            get { return _vim.VimData; }
        }

        internal IVimBufferFactory VimBufferFactory
        {
            get { return _vimBufferFactory; }
        }

        public MockVimHost VimHost
        {
            get { return (MockVimHost)_vim.VimHost; }
        }

        public ICommonOperationsFactory CommonOperationsFactory
        {
            get { return _commonOperationsFactory; }
        }

        public IWordUtil WordUtil
        {
            get { return _wordUtil; }
        }

        public IFoldManagerFactory FoldManagerFactory
        {
            get { return _foldManagerFactory; }
        }

        public IBufferTrackingService BufferTrackingService
        {
            get { return _bufferTrackingService; }
        }

        public IKeyMap KeyMap
        {
            get { return _vim.KeyMap; }
        }

        public IKeyUtil KeyUtil
        {
            get { return _keyUtil; }
        }

        public IClipboardDevice ClipboardDevice
        {
            get { return _clipboardDevice; }
        }

        public IMouseDevice MouseDevice
        {
            get { return _mouseDevice; }
        }

        public IKeyboardDevice KeyboardDevice
        {
            get { return _keyboardDevice; }
        }

        public IVimProtectedOperations VimProtectedOperations
        {
            get { return _vimProtectedOperations; }
        }

        public IVimErrorDetector VimErrorDetector
        {
            get { return _vimErrorDetector; }
        }

        internal IBulkOperations BulkOperations
        {
            get { return _bulkOperations; }
        }

        public IEditorFormatMapService EditorFormatMapService
        {
            get { return _editorFormatMapService; }
        }

        public IClassificationFormatMapService ClassificationFormatMapService
        {
            get { return _classificationFormatMapService; }
        }

        public VimEditorHost(CompositionContainer compositionContainer) : base(compositionContainer)
        {
            _vim = CompositionContainer.GetExportedValue<IVim>();
            _vimBufferFactory = CompositionContainer.GetExportedValue<IVimBufferFactory>();
            _vimErrorDetector = CompositionContainer.GetExportedValue<IVimErrorDetector>();
            _commonOperationsFactory = CompositionContainer.GetExportedValue<ICommonOperationsFactory>();
            _wordUtil = CompositionContainer.GetExportedValue<IWordUtil>();
            _bufferTrackingService = CompositionContainer.GetExportedValue<IBufferTrackingService>();
            _foldManagerFactory = CompositionContainer.GetExportedValue<IFoldManagerFactory>();
            _bulkOperations = CompositionContainer.GetExportedValue<IBulkOperations>();
            _keyUtil = CompositionContainer.GetExportedValue<IKeyUtil>();
            _vimProtectedOperations = CompositionContainer.GetExportedValue<IVimProtectedOperations>();

            _keyboardDevice = CompositionContainer.GetExportedValue<IKeyboardDevice>();
            _mouseDevice = CompositionContainer.GetExportedValue<IMouseDevice>();
            _clipboardDevice = CompositionContainer.GetExportedValue<IClipboardDevice>();
            _editorFormatMapService = CompositionContainer.GetExportedValue<IEditorFormatMapService>();
            _classificationFormatMapService = CompositionContainer.GetExportedValue<IClassificationFormatMapService>();
        }
    }
}
