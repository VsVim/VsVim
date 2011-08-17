#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining

type OperationsData = {
    EditorOperations : IEditorOperations
    EditorOptions : IEditorOptions
    FoldManager : IFoldManager
    JumpList : IJumpList
    KeyMap : IKeyMap
    LocalSettings : IVimLocalSettings
    OutliningManager : IOutliningManager option
    RegisterMap : IRegisterMap 
    SearchService : ISearchService
    StatusUtil : IStatusUtil
    TextView : ITextView
    UndoRedoOperations : IUndoRedoOperations
    VimData : IVimData
    VimHost : IVimHost
    WordUtil : IWordUtil
}

module internal CommonUtil =

    /// Select the given VisualSpan in the ITextView.
    val Select : ITextView -> VisualSpan -> unit

    /// Select the given VisualSelection in the ITextView and place the caret in the correct
    /// position
    val SelectAndUpdateCaret : ITextView -> VisualSelection -> unit

    val RaiseSearchResultMessage : IStatusUtil -> SearchResult -> unit

