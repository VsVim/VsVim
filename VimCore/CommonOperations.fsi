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

    val RaiseSearchResultMessage : IStatusUtil -> SearchResult -> unit

