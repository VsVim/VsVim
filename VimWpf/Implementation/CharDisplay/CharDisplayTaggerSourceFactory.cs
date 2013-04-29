using EditorUtils;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

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

        [ImportingConstructor]
        internal CharDisplayTaggerSourceFactory([EditorUtilsImport]ITaggerFactory taggerFactory)
        {
            _taggerFactory = taggerFactory;
        }

        // TODO: should only do this for vim supported buffers

        #region IViewTaggerProvider

        ITagger<T> IViewTaggerProvider.CreateTagger<T>(ITextView textView, ITextBuffer textBuffer)
        {
            return _taggerFactory.CreateBasicTagger(
                textView.Properties,
                _key,
                () => new CharDisplayTaggerSource(textView)) as ITagger<T>;
        }

        #endregion
    }

/*
[<Export(typeof<IViewTaggerProvider>)>]
[<ContentType(Constants.AnyContentType)>]
[<TextViewRole(PredefinedTextViewRoles.Document)>]
[<TagType(typeof<TextMarkerTag>)>]
type HighlightIncrementalSearchTaggerProvider
    [<ImportingConstructor>]
    ( 
        _vim : IVim,
        [<EditorUtilsImport>] _taggerFactory : ITaggerFactory
    ) = 

    let _key = obj()

    interface IViewTaggerProvider with 
        member x.CreateTagger<'T when 'T :> ITag> ((textView : ITextView), textBuffer) = 
            if textView.TextBuffer = textBuffer then
                match _vim.GetOrCreateVimBufferForHost textView with
                | None -> null
                | Some vimBuffer ->
                    let tagger = _taggerFactory.CreateAsyncTagger(textView.Properties, _key, fun () ->
                        let wordNavigator = vimBuffer.WordNavigator
                        let taggerSource = new HighlightSearchTaggerSource(textView, vimBuffer.GlobalSettings, _vim.VimData, _vim.VimHost)
                        taggerSource :> IAsyncTaggerSource<HighlightSearchData , TextMarkerTag>)
                    tagger :> obj :?> ITagger<'T>
            else
                null
    */
}
