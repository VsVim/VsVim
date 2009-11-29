#light

namespace VimCore
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions

module Utils =

    let GetRegexOptions (opt:SearchOptions) =  
        let mutable regexOptions = RegexOptions.Compiled
        let opt = LanguagePrimitives.EnumToValue opt
        if 0 <> (opt &&& LanguagePrimitives.EnumToValue SearchOptions.IgnoreCase) then
            regexOptions <- regexOptions &&& RegexOptions.IgnoreCase
        regexOptions

    /// Try and build a regex expression from the specified pattern.  Return an 
    /// empty option if building it is not possible
    let SafeBuildRegex pattern opt = 
        try
            let regexOpt = GetRegexOptions opt
            let r = new Regex(pattern, regexOpt)
            Some r
        with 
            | :? System.ArgumentException -> None

type IncrementalSearch(_pattern:string,_kind:SearchKind, _options: SearchOptions) =
    let _regex = Utils.SafeBuildRegex _pattern _options
    new (pattern) = IncrementalSearch(pattern, SearchKind.ForwardWithWrap, SearchOptions.None)
    new (pattern,kind) = IncrementalSearch(pattern, kind, SearchOptions.None)
    member x.Pattern = _pattern
    member x.SearchKind = _kind
    member x.Regex = _regex
    
    member x.FilterSpanCore (tss:ITextSnapshot) (span:SnapshotSpan) (regex:Regex) =
        let validMatch (m:Match) = match m.Success with | true -> Some m | false -> None
        let forward text = 
            let m = regex.Match(text)
            validMatch m
        let reverse text = 
            let col = regex.Matches(text)
            let lastIndex = col.Count - 1
            match lastIndex with | -1 -> None | _ -> validMatch col.[lastIndex]
            
        let text = span.GetText()
        let find = TssUtil.SearchDirection _kind forward reverse
        let found = find text
        match found with
            | None -> None
            | Some m-> 
                let capture = m.Groups.[0]
                let start = span.Start.Add(capture.Index)
                let span = new SnapshotSpan(start, capture.Length)
                Some (span)
               
    member x.FilterSpan (tss:ITextSnapshot) (span:SnapshotSpan) =
        match _regex with 
            | Some r -> x.FilterSpanCore tss span r
            | None -> None               
        
    /// Find the next match in the snapshot based on the passed in position
    member x.FindNextMatch point =
        let rec findFirstBlank (point:SnapshotPoint) =
            let line = point.GetContainingLine()
            match point.Position >= line.End.Position with 
                | true -> line.End
                | false -> 
                    match System.Char.IsWhiteSpace(point.GetChar()) with
                        | true -> point
                        | false -> findFirstBlank (point.Add(1))
        let start = 
            match SearchKindUtil.IsBackward _kind with
                | true -> findFirstBlank point
                | false -> point
        TssUtil.GetSpans start _kind 
            |> Seq.tryPick(x.FilterSpan point.Snapshot)
        

