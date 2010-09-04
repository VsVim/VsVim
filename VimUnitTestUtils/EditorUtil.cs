using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Vim;

namespace Vim.UnitTest
{
    public static class EditorUtil
    {
        #region Factory

        public sealed class Factory
        {
            [Import]
            public ITextBufferFactoryService textBufferFactory;

            [Import]
            public ITextEditorFactoryService textEditorFactory;

            [Import]
            public IEditorOperationsFactoryService editorOperationsFactory;

            [Import]
            public IEditorOptionsFactoryService editorOptionsFactory;

            [Import]
            public ISignatureHelpBroker signatureBroker;

            [Import]
            public ICompletionBroker completionBroker;

            [Import]
            public IEditorFormatMapService editorFormatMapService;

            [Import]
            public IVim vim;

            [Import]
            public IVimHost vimHost;

            [Import]
            public ITextSearchService textSearchService;

            [Import]
            public IVimBufferFactory vimBufferFactory;

            [Import]
            public ITextBufferUndoManagerProvider undoManagerProvider;

            [Import]
            public IContentTypeRegistryService contentTypeRegistryService;

            public Factory() { }
        }

        #endregion

        [ThreadStatic]
        private static CompositionContainer m_container;
        [ThreadStatic]
        private static Factory m_factory;

        public static CompositionContainer Container
        {
            get
            {
                if (null == m_container)
                {
                    m_container = CreateContainer();
                }
                return m_container;
            }
        }

        public static Factory FactoryService
        {
            get
            {

                if (null == m_factory)
                {
                    m_factory = new Factory();
                    Container.ComposeParts(m_factory);
                }
                return m_factory;
            }
        }
        public static ITextBuffer CreateBuffer(params string[] lines)
        {
            return CreateBuffer(null, lines);
        }

        public static ITextBuffer CreateBuffer(IContentType contentType, params string[] lines)
        {
            var factory = FactoryService.textBufferFactory;
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
            var view = FactoryService.textEditorFactory.CreateTextView(buffer);
            return view;
        }

        public static Tuple<IWpfTextView, IEditorOperations> CreateViewAndOperations(params string[] lines)
        {
            var view = CreateView(lines);
            var opts = FactoryService.editorOperationsFactory.GetEditorOperations(view);
            return Tuple.Create(view, opts);
        }

        public static IEditorOperations GetOperations(IWpfTextView view)
        {
            return FactoryService.editorOperationsFactory.GetEditorOperations(view);
        }

        public static ITextUndoHistory GetUndoHistory(ITextBuffer textBuffer)
        {
            return FactoryService.undoManagerProvider.GetTextBufferUndoManager(textBuffer).TextBufferUndoHistory;
        }

        public static IContentType GetOrCreateContentType(string type, string baseType)
        {
            var ct = FactoryService.contentTypeRegistryService.GetContentType(type);
            if (ct == null)
            {
                ct = FactoryService.contentTypeRegistryService.AddContentType(type, new string[] { baseType });
            }

            return ct;
        }

        public static CompositionContainer CreateContainer()
        {
            try
            {
                var uri = new Uri(typeof(EditorUtil).Assembly.CodeBase);
                var root = Path.GetDirectoryName(uri.LocalPath);
                var list = new List<ComposablePartCatalog>();
                list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Platform.VSEditor.dll")));

                // Not entirely sure why this is suddenly needed
                list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.Internal.dll")));

                // Must include this because several editor options are actually stored as exported information 
                // on this DLL.  Including most importantly, the tabsize information
                list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.Logic.dll")));

                // Include this DLL to get several more EditorOptions including WordWrapStyle
                list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.UI.dll")));

                // Include this DLL to get more EditorOptions values
                list.Add(new AssemblyCatalog(Path.Combine(root, "Microsoft.VisualStudio.Text.UI.Wpf.dll")));

                // There is no default IUndoHistoryRegistry provided so I need to provide it here just to 
                // satisfy the MEF import.  
                list.Add(new AssemblyCatalog(typeof(EditorUtil).Assembly));

                // IBlockCaret needs to be satisfied for integration tests
                list.Add(new AssemblyCatalog(typeof(IVim).Assembly));

                var catalog = new AggregateCatalog(list.ToArray());
                return new CompositionContainer(catalog);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
