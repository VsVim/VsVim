#light
namespace Vim
open System.Diagnostics

/// Represents the different type of operations that are available for Motions
[<DebuggerDisplay("{ToString(),nq}")>]
type OperationKind = 
    | CharacterWise 
    | LineWise 

    with

    override x.ToString() =
        match x with 
        | CharacterWise -> "CharacterWise"
        | LineWise -> "LineWise"
