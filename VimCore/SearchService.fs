#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Editor

type internal SearchService 
    (
        _search : ITextSearchService,
        _settings : IVimGlobalSettings ) = 

    let mutable _lastSearch = { Text = Pattern(StringUtil.empty); Kind = SearchKind.ForwardWithWrap; Options = SearchOptions.None }
    let _lastSearchChanged = Event<SearchData>()

    member private x.CreateFindOptions (text:SearchText) kind searchOptions =
        let caseOptions = 
            if _settings.IgnoreCase && Utils.IsFlagSet searchOptions SearchOptions.AllowIgnoreCase then
                let hasUpper () = text.RawText |> Seq.filter CharUtil.IsLetter |> Seq.filter CharUtil.IsUpper |> SeqUtil.isNotEmpty
                if _settings.SmartCase && Utils.IsFlagSet searchOptions SearchOptions.AllowSmartCase && hasUpper() then FindOptions.MatchCase
                else FindOptions.None
            else FindOptions.MatchCase
        let revOptions = if SearchKindUtil.IsBackward kind then FindOptions.SearchReverse else FindOptions.None

        let searchKindOptions = 
            match text with
            | Pattern(_) -> FindOptions.UseRegularExpressions
            | WholeWord(_) -> FindOptions.WholeWord
            | StraightText(_) -> FindOptions.None

        caseOptions ||| revOptions ||| searchKindOptions

    member private x.FindNext (searchData:SearchData) point nav =
        let tss = SnapshotPointUtil.GetSnapshot point
        let pos = SnapshotPointUtil.GetPosition point
        let opts = x.CreateFindOptions searchData.Text searchData.Kind searchData.Options
        let findData = FindData(searchData.Text.RawText, tss, opts, nav) 
        _search.FindNext(pos, (SearchKindUtil.IsWrap searchData.Kind), findData) |> NullableUtil.toOption

    interface ISearchService with
        member x.LastSearch 
            with get() = _lastSearch
            and set value = 
                _lastSearch <- value
                _lastSearchChanged.Trigger value
        [<CLIEvent>]
        member x.LastSearchChanged = _lastSearchChanged.Publish
        member x.FindNext searchData point nav = x.FindNext searchData point nav


