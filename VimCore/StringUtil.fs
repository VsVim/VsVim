#light


namespace Vim

module internal StringUtil =

    let FindFirst (input:seq<char>) index del =
        let found = 
            input 
                |> Seq.mapi ( fun i c -> (i,c) )
                |> Seq.skip index 
                |> Seq.skipWhile (fun (i,c) -> not (del c))
        match Seq.isEmpty found with
            | true -> None
            | false -> Some (Seq.head found)
            
    let IsValidIndex index (input:string) = index >= 0 && index < input.Length
            
    let CharAtOption index (input:string) = 
        match IsValidIndex index input with
            | true -> Some input.[index]
            | false -> None
            
    let CharAt index input =
        match CharAtOption index input with 
            | Some c -> c
            | None -> failwith "Invalid index"
    
    let Repeat (value:string) count =
        if 1 = count then value
        else
            let buffer = new System.Text.StringBuilder()
            for i = 1 to count do
                buffer.Append(value) |> ignore
            buffer.ToString()

    /// Create a String from an array of chars
    [<CompiledName("OfCharArray")>]
    let ofCharArray (chars:char[]) = new System.String(chars)

    /// Create a String from a sequence of chars
    [<CompiledName("OfCharSeq")>]
    let ofCharSeq (chars : char seq) = chars |> Array.ofSeq |> ofCharArray

    /// Create a String from a single char
    [<CompiledName("OfChar")>]
    let ofChar c = System.String(c,1)

    [<CompiledName("IsNullOrEmpty")>]
    let isNullOrEmpty str = System.String.IsNullOrEmpty(str)

    let Length (str:string) = 
        if str = null then 0
        else str.Length

    let IsEqualIgnoreCase left right = 
        let comp = System.StringComparer.OrdinalIgnoreCase
        comp.Equals(left,right)

    let IsEqual left right = 
        let comp = System.StringComparer.Ordinal
        comp.Equals(left,right)
