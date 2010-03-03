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

    let OfCharArray (chars:char[]) = new System.String(chars)

    let OfCharSeq (chars : char seq) = chars |> Array.ofSeq |> OfCharArray

    let Length (str:string) = 
        if str = null then 0
        else str.Length

    let IsEqualIgnoreCase left right = 
        let comp = System.StringComparer.OrdinalIgnoreCase
        comp.Equals(left,right)

    let IsEqual left right = 
        let comp = System.StringComparer.Ordinal
        comp.Equals(left,right)
