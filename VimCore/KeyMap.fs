#light

namespace Vim

type internal RemapModeMap = Map<KeyInput, (KeyInput seq * bool)>

type internal KeyMap() =
    
    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    member x.GetKeyMapping ki mode = 
        match _map |> Map.tryFind mode with
        | None -> Seq.singleton ki
        | Some(modeMap) ->  
            match modeMap |> Map.tryFind ki with
            | None -> Seq.singleton ki
            | Some(kiSeq,_) -> kiSeq

    member x.MapWithNoRemap (lhs:string) (rhs:string) (mode:KeyRemapMode) = 
        if StringUtil.Length lhs <> 1 || StringUtil.Length rhs <= 0 then 
            false
        else
            let lhs = x.ParseKeyBinding lhs
            let rhs = x.ParseKeyBinding rhs
            match lhs,rhs with
            | Some(leftSeq),Some(rightSeq) ->
                let leftKi = leftSeq |> Seq.head
                let value = (rightSeq,false)
                let modeMap = 
                    match _map |> Map.tryFind mode with
                    | None -> Map.empty
                    | Some(modeMap) -> modeMap
                let modeMap = Map.add leftKi value modeMap
                _map <- Map.add mode modeMap _map
                true
            | _ -> false

    /// Parse out the passed in key bindings.  Returns None in the case of a bad
    /// format on data or a Some KeyInput list on success
    member private x.ParseKeyBinding (data:string) =
        let hasBadData = 
            data 
            |> Seq.filter (fun c -> not (System.Char.IsLetterOrDigit(c)))
            |> SeqUtil.isNotEmpty
        if hasBadData then None
        else data |> Seq.map InputUtil.CharToKeyInput |> Some
            

    interface IKeyMap with
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
