#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations
open CharUtil

module TssUtil =

    /// Vim is fairly odd in that it considers the top line of the file to be both line numbers
    /// 1 and 0.  The next line is 2.  The editor is a zero based index though so we need
    /// to take that into account
    let VimLineToTssLine line = 
        match line with
        | 0 -> 0
        | _ -> line - 1

    /// Wrap the TextUtil functions which operate on String and int locations into 
    /// a SnapshotPoint and SnapshotSpan version
    let WrapTextSearch (func: WordKind -> string -> int -> option<Span> ) = 
        let f kind point = 
            let line = SnapshotPointUtil.GetContainingLine point
            let text = line.GetText()
            let pos = point.Position - line.Start.Position
            match pos >= text.Length with
            | true -> None
            | false ->
                match func kind text pos with
                | Some s ->
                    let adjusted = new Span(line.Start.Position+s.Start, s.Length)
                    Some (new SnapshotSpan(point.Snapshot, adjusted))
                | None -> 
                    None
        f

    let FindCurrentWordSpan point kind = (WrapTextSearch TextUtil.FindCurrentWordSpan) kind point
    let FindCurrentFullWordSpan point kind = (WrapTextSearch TextUtil.FindFullWordSpan) kind point

    let CreateTextStructureNavigator wordKind (baseImpl:ITextStructureNavigator) = 
        { new ITextStructureNavigator with 
            member x.ContentType = baseImpl.ContentType
            member x.GetExtentOfWord point = 
                match FindCurrentFullWordSpan point wordKind with
                | Some(span) -> TextExtent(span, true)
                | None -> TextExtent(SnapshotSpan(point,1),false)
            member x.GetSpanOfEnclosing span = baseImpl.GetSpanOfEnclosing(span)
            member x.GetSpanOfFirstChild span = baseImpl.GetSpanOfFirstChild(span)
            member x.GetSpanOfNextSibling span = baseImpl.GetSpanOfNextSibling(span)
            member x.GetSpanOfPreviousSibling span = baseImpl.GetSpanOfPreviousSibling(span) }

