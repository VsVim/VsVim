#light
namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Operations


/// Used for the paragraph motion.  
[<RequireQualifiedAccess>]
type Paragraph =
    /// Actual content of the paragraph.  
    | Content of SnapshotSpan

    /// The ITextSnapshotLine which is a boundary item for the paragraph
    | Boundary of int * SnapshotSpan

    with 

    /// What is the Span of this paragraph 
    member x.Span = 
        match x with 
        | Paragraph.Content(span) -> span
        | Paragraph.Boundary(_,span) -> span

    member x.ReduceToSpan span =
        match x with
        | Paragraph.Content(_) -> Paragraph.Content(span)
        | Paragraph.Boundary(line,_) -> Paragraph.Boundary(line,span)


