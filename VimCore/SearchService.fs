#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _settings : IVimGlobalSettings ) = 

    let mutable _lastSearch = { Pattern = System.String.Empty; Kind = SearchKind.ForwardWithWrap; Options = SearchOptions.None }
    let _lastSearchChanged = Event<SearchData>()

    member private x.CreateFindOptions kind searchOptions =
        let caseOptions = if not _settings.IgnoreCase then FindOptions.MatchCase else FindOptions.None
        let revOptions = if SearchKindUtil.IsBackward kind then FindOptions.SearchReverse else FindOptions.None
        let wordOptions = if Utils.IsFlagSet searchOptions SearchOptions.MatchWord then FindOptions.WholeWord else FindOptions.None
        let regexOptions = if Utils.IsFlagSet searchOptions SearchOptions.Regex then FindOptions.UseRegularExpressions else FindOptions.None
        caseOptions ||| revOptions ||| wordOptions ||| regexOptions

    member private x.CreateSearchData pattern kind = 
        { Pattern = pattern; Kind = kind; Options = SearchOptions.None }

    member private x.CreateSearchDataWithOptions pattern kind options = 
        { Pattern = pattern; Kind = kind; Options = options }

    member private x.FindNextPattern pattern point kind nav = 
        let data = x.CreateSearchDataWithOptions pattern kind SearchOptions.Regex
        x.FindNextResult data point nav

    member private x.FindNextResult (searchData:SearchData) point nav = 
        let tss = SnapshotPointUtil.GetSnapshot point
        let pos = SnapshotPointUtil.GetPosition point
        let opts = x.CreateFindOptions searchData.Kind searchData.Options
        let findData = FindData(searchData.Pattern, tss, opts, nav) 
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
        member x.CreateSearchDataWithOptions pattern kind extraOptions = x.CreateSearchDataWithOptions pattern kind extraOptions
        member x.FindNextPattern pattern point kind nav = x.FindNextPattern pattern point kind nav
        member x.FindNextResult searchData point nav = x.FindNextResult searchData point nav


