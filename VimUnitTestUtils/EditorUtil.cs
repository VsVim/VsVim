using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Vim.UnitTest.Exports;
using IOPath = System.IO.Path;

namespace Vim.UnitTest
{
    // TODO: Need to change this so that creating an IVimBuffer doesn't read my _vsvimrc file
    public static class EditorUtil
    {
        #region Factory

        public sealed class Factory
        {
            [Import]
            public ITextBufferFactoryService TextBufferFactory;

            [Import]
            public ITextEditorFactoryService TextEditorFactory;

            [Import]
            public IEditorOperationsFactoryService EditorOperationsFactory;

            [Import]
            public IEditorOptionsFactoryService EditorOptionsFactory;

            [Import]
            public IVim Vim;

            [Import]
            public IVimHost VimHost;

            [Import]
            public ITextSearchService TextSearchService;

            [Import]
            public IVimBufferFactory VimBufferFactory;

            [Import]
            public ITextBufferUndoManagerProvider UndoManagerProvider;

            [Import]
            public IContentTypeRegistryService ContentTypeRegistryService;

            [Import]
            public ITextStructureNavigatorSelectorService TextStructureNavigatorSelectorService;

            [Import] 
            public ISmartIndentationService SmartIndentationService;

            [Import]
            public ICompletionBroker CompletionBroker;
        }

        #endregion

        [ThreadStatic]
        private static CompositionContainer _compositionContainer;
        [ThreadStatic]
        private static Factory _factory;

        public static CompositionContainer Container
        {
            get
            {
                if (null == _compositionContainer)
                {
                    _compositionContainer = CreateContainer();
                }
                return _compositionContainer;
            }
        }

        public static Factory FactoryService
        {
            get
            {

                if (null == _factory)
                {
                    _factory = new Factory();
                    Container.ComposeParts(_factory);
                }
                return _factory;
            }
        }
        public static ITextBuffer CreateBuffer(params string[] lines)
        {
            return CreateBuffer(null, lines);
        }

        public static ITextBuffer CreateBuffer(IContentType contentType, params string[] lines)
        {
            var factory = FactoryService.TextBufferFactory;
            var buffer = contentType != null
                ? factory.CreateTextBuffer(contentType)
                : factory.CreateTextBuffer();

            if (lines.Length != 0)
            {
                var text = lines.Aggregate((x, y) => x + Environment.NewLine + y);
                buffer.Replace(new Span(0, 0), text);
            }

            return buffer;
        }

        public static IWpfTextView CreateView(params string[] lines)
        {
            return CreateView(null, lines);
        }

        public static IWpfTextView CreateView(IContentType contentType, params string[] lines)
        {
            var buffer = CreateBuffer(contentType, lines);
            var view = FactoryService.TextEditorFactory.CreateTextView(buffer);
            return view;
        }

        public static Tuple<IWpfTextView, IEditorOperations> CreateViewAndOperations(params string[] lines)
        {
            var view = CreateView(lines);
            var opts = FactoryService.EditorOperationsFactory.GetEditorOperations(view);
            return Tuple.Create(view, opts);
        }

        public static IEditorOperations GetOperations(ITextView view)
        {
            return FactoryService.EditorOperationsFactory.GetEditorOperations(view);
        }

        public static ITextUndoHistory GetUndoHistory(ITextBuffer textBuffer)
        {
            return FactoryService.UndoManagerProvider.GetTextBufferUndoManager(textBuffer).TextBufferUndoHistory;
        }

        public static IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = FactoryService.ContentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = FactoryService.ContentTypeRegistryService.AddContentType(type, new string[] { baseType });
            }

            return ct;
        }

        public static List<ComposablePartCatalog> GetEditorCatalog()
        {
            var uri = new Uri(typeof(EditorUtil).Assembly.CodeBase);
            var root = IOPath.GetDirectoryName(uri.LocalPath);
            var list = new List<ComposablePartCatalog>();
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Platform.VSEditor.dll")));

            // Not entirely sure why this is suddenly needed
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.Internal.dll")));

            // Must include this because several editor options are actually stored as exported information 
            // on this DLL.  Including most importantly, the tabsize information
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.Logic.dll")));

            // Include this DLL to get several more EditorOptions including WordWrapStyle
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.UI.dll")));

            // Include this DLL to get more EditorOptions values
            list.Add(new AssemblyCatalog(IOPath.Combine(root, "Microsoft.VisualStudio.Text.UI.Wpf.dll")));

            // There is no default IUndoHistoryRegistry provided so I need to provide it here just to 
            // satisfy the MEF import.  
            list.Add(new TypeCatalog(typeof(TextUndoHistoryRegistry)));

            return list;
        }

        public static List<ComposablePartCatalog> GetUnitTestCatalog()
        {
            var list = GetEditorCatalog();

            // IBlockCaret needs to be satisfied for integration tests
            list.Add(new AssemblyCatalog(typeof(IVim).Assembly));

            // Other Exports needed to construct VsVim
            list.Add(new TypeCatalog(
                typeof(ClipboardDevice),
                typeof(KeyboardDevice),
                typeof(MouseDevice),
                typeof(VimHost)));

            return list;
        }

        public static CompositionContainer CreateContainer()
        {
            var list = GetUnitTestCatalog();
            var catalog = new AggregateCatalog(list.ToArray());
            return new CompositionContainer(catalog);
        }
    }
}
