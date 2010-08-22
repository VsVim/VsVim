#light

namespace Vim
open Microsoft.VisualStudio.Text

[<RequireQualifiedAccess>]
type StringData = 
    | Simple of string
    | Block of string list 
    with 

    // TODO: Delete this and force the use of individual values
    member x.String =
        match x with 
        | Simple(str) -> str
        | Block(l) -> l |> StringUtil.combineWith System.Environment.NewLine

    static member ofNormalizedSnasphotSpanCollection (col:NormalizedSnapshotSpanCollection) = 
        if col.Count = 1 then col.[0] |> SnapshotSpanUtil.GetText |> StringData.Simple
        else 
            col
            |> Seq.map SnapshotSpanUtil.GetText
            |> List.ofSeq
            |> StringData.Block

    static member ofSpan span = span |> SnapshotSpanUtil.GetText |> StringData.Simple

/// Value stored in the register.  Contains contextual information on how the data
/// was yanked from the buffer
type RegisterValue = {
    Value : StringData;
    MotionKind : MotionKind;
    OperationKind : OperationKind;
}

type Register(_name:char) = 
    let mutable _value = { Value= StringData.Simple StringUtil.empty; MotionKind=MotionKind.Inclusive; OperationKind=OperationKind.CharacterWise}
    member x.Name = _name
    member x.StringValue = _value.Value.String
    member x.Value = _value
    member x.UpdateValue v = _value <- v


