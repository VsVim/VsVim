#light

namespace Vim
open Microsoft.VisualStudio.Text
open System.Windows.Input
open System.Text.RegularExpressions

module internal RegexUtil =
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
