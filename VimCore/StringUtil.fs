#light


namespace VimCore

module StringUtil =
    let FindFirst (input:seq<char>) index del =
        let found = 
            input 
                |> Seq.mapi ( fun i c -> (i,c) )
                |> Seq.skip index 
                |> Seq.skipWhile (fun (i,c) -> not (del c))
        match Seq.isEmpty found with
            | true -> None
            | false -> Some (Seq.head found)
            
    let IsValidIndex (input:string) index = index >= 0 && index < input.Length
            
    let CharAtOption (input:string) index = 
        match IsValidIndex input index with
            | true -> Some input.[index]
            | false -> None
            
    let CharAt input index =
        match CharAtOption input index with 
            | Some c -> c
            | None -> failwith "Invalid index"
    
    let Repeat (value:string) count =
        let buffer = new System.Text.StringBuilder()
        for i = 1 to count do
            buffer.Append(value) |> ignore
        buffer.ToString()
