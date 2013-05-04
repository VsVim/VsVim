#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Text.RegularExpressions

module internal RegexPatternUtil =
    let group (m:Match) (n:int) = m.Groups.[n].Value
    let (|MatchAll|_|) (pat:string) (input:string) = 
        let m = Regex.Match(input,pat) 
        match m.Success with
        | false -> None
        | true -> Some ([for g in m.Groups -> g.Value])
        
    let (|Match1|_|) (pat:string) (input:string) = 
        let m = Regex.Match(input,pat)
        match m.Success && m.Groups.Count=1 with
        | false -> None
        | true -> Some (group m 0)
            
    let (|Match2|_|) (pat:string) (input:string) = 
        let m = Regex.Match(input,pat)
        match m.Success && m.Groups.Count=2 with
        | false -> None
        | true -> Some ((group m 0),(group m 1))

    let (|Match3|_|) (pat:string) (input:string) = 
        let m = Regex.Match(input,pat)
        match m.Success && m.Groups.Count=3 with
        | false -> None
        | true -> Some ((group m 0),(group m 1),(group m 2))


/// APIs to make it easy to integrate Regex's into the Editor APIs
module RegexUtil = 

    let MatchSpan (span:SnapshotSpan) (regex:Regex) =
        let text = span.GetText()
        let capture = regex.Match text
        if capture.Success then 
            let point = SnapshotPointUtil.Add capture.Index span.Start
            let span = SnapshotSpanUtil.CreateWithLength point capture.Length
            Some (span,capture)
        else 
            None

