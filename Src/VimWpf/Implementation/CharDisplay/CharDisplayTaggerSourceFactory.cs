using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Classification;

namespace Vim.UI.Wpf.Implementation.CharDisplay
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType(Constants.AnyContentType)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class CharDisplayTaggerSourceFactory : IViewTaggerProvider
    {
        private readonly object _key = new object();
        private readonly ITaggerFactory _taggerFactory;
        private readonly IEditorFormatMapService _editorFormatMapService;

        [ImportingConstructor]
        internal CharDisplayTaggerSourceFactory([EditorUtilsImport]ITaggerFactory taggerFactory, IEditorFormatMapService editorFormatMapService)
        {
            _taggerFactory = taggerFactory;
            _editorFormatMapService = editorFormatMapService;
        }

        private CharDisplayTaggerSource CreateCharDisplayTaggerSource(ITextView textView)
        {
            var editorFormatMap = _editorFormatMapService.GetEditorFormatMap(textView);
            return new CharDisplayTaggerSource(textView, editorFormatMap);
        }

        // TODO: should only do this for vim supported buffers

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            return _taggerFactory.CreateBasicTagger(
                textView.Properties,
                _key,
                () => CreateCharDisplayTaggerSource(textView)) as ITagger<T>;
        }

        #endregion
    }
}
