#light

namespace Vim
open Microsoft.VisualStudio.Text
open System
open System.Text.RegularExpressions

type internal RegexSearchReplace() = 
    
    /// Try and build a regex expression from the specified pattern.  Return an 
    /// empty option if building it is not possible
    static member private TryCreateRegex (searchData:SearchData) = 
        let options = 
            if Utils.IsFlagSet (searchData.Flags) SearchReplaceFlags.IgnoreCase then
                RegexOptions.IgnoreCase
            else
                RegexOptions.None
        let options = options &&& RegexOptions.Compiled
        try
            let r = new Regex(searchData.Pattern, options)
            Some r
        with 
            | :? System.ArgumentException -> None

    /// This method filters out spans looking for valid regex matches within the Span.  It will return
    /// an empty option if no match occurs on the span, otherwise the span of the match
    member private x.FilterSpan (regex:Regex) (kind:SearchKind) (span:SnapshotSpan) = 
        let validMatch (m:Match) = match m.Success with | true -> Some m | false -> None
        let forward text = 
            let m = regex.Match(text)
            validMatch m
        let reverse text = 
            let col = regex.Matches(text)
            let lastIndex = col.Count - 1
            match lastIndex with | -1 -> None | _ -> validMatch col.[lastIndex]
            
        let text = span.GetText()
        let find = TssUtil.SearchDirection kind forward reverse
        let found = find text
        match found with
            | None -> None
            | Some m-> 
                let capture = m.Groups.[0]
                let start = span.Start.Add(capture.Index)
                let span = new SnapshotSpan(start, capture.Length)
                Some (span)
               
    /// Find the next match in the snapshot based on the passed in position
    member x.FindNextMatch searchData point =
        match RegexSearchReplace.TryCreateRegex searchData with
        | None -> None
        | Some(regex) ->
            let kind = searchData.Kind
            let rec findFirstBlank (point:SnapshotPoint) =
                let line = point.GetContainingLine()
                match point.Position >= line.End.Position with 
                    | true -> line.End
                    | false -> 
                        match System.Char.IsWhiteSpace(point.GetChar()) with
                            | true -> point
                            | false -> findFirstBlank (point.Add(1))
            let start = 
                match SearchKindUtil.IsBackward kind with
                | true -> findFirstBlank point
                | false -> point
            TssUtil.GetSpans start kind 
                |> Seq.tryPick (x.FilterSpan regex kind)

    member x.FindNextWord point wordKind searchKind ignoreCase = 
        match TssUtil.FindCurrentFullWordSpan point wordKind with
        | Some(wordSpan) ->
            let comp = if ignoreCase then System.StringComparer.OrdinalIgnoreCase else System.StringComparer.Ordinal
            let word = wordSpan.GetText()
            TssUtil.GetWordSpans wordSpan.End wordKind searchKind  |> Seq.tryFind (fun span -> comp.Equals(span.GetText(), word))
        | None -> None
        
    interface ISearchReplace with
        member x.FindNextMatch searchData point = x.FindNextMatch searchData point
        member x.FindNextWord point wordKind searchKind ignoreCase = x.FindNextWord point wordKind searchKind ignoreCase  
