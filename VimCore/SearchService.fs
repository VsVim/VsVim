#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _settings : IVimGlobalSettings ) = 

    let mutable _lastSearch = { Pattern = System.String.Empty; Kind = SearchKind.ForwardWithWrap; Options = FindOptions.None }
    let _lastSearchChanged = Event<SearchData>()

    member private x.CreateFindOptions kind =
        let options = if not _settings.IgnoreCase then FindOptions.MatchCase else FindOptions.None
        let options = if SearchKindUtil.IsBackward kind then options ||| FindOptions.SearchReverse else options
        options

    member private x.CreateSearchData pattern kind = 
        let options = x.CreateFindOptions kind 
        { Pattern = pattern; Kind = kind; Options = options }

    member private x.FindNextPattern pattern point kind nav = 
        let data = x.CreateSearchData pattern kind
        x.FindNextResult data point nav

    member private x.FindNextResult (searchData:SearchData) point nav = 
        let tss = SnapshotPointUtil.GetSnapshot point
        let pos = SnapshotPointUtil.GetPosition point
        let findData = FindData(searchData.Pattern, tss, searchData.Options, nav) 
        _search.FindNext(pos, (SearchKindUtil.IsWrap searchData.Kind), findData) |> NullableUtil.toOption

    interface ISearchService with
        member x.LastSearch 
            with get() = _lastSearch
            and set value = 
                _lastSearch <- value
                _lastSearchChanged.Trigger value
        [<CLIEvent>]
        member x.LastSearchChanged = _lastSearchChanged.Publish
        member x.CreateSearchData pattern kind = x.CreateSearchData pattern kind
        member x.FindNextPattern pattern point kind nav = x.FindNextPattern pattern point kind nav
        member x.FindNextResult searchData point nav = x.FindNextResult searchData point nav


