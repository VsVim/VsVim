#light

namespace Vim

type internal RemapModeMap = Map<KeyInput, (KeyInput seq * bool)>

type internal KeyMap() =
    
    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    member x.GetKeyMapping ki mode = 
        let kiSeq,_,_ = x.GetKeyMappingCore ki mode Set.empty
        kiSeq

    member x.GetKeyMappingResult ki mode = 
        let kiSeq,isRecursive,_ = x.GetKeyMappingCore ki mode Set.empty
        if isRecursive then RecursiveMapping
        elif kiSeq |> Seq.isEmpty then NoMapping
        elif kiSeq |> Seq.length = 1 then SingleKey (kiSeq |> Seq.head)
        else KeySequence kiSeq

    member x.MapWithNoRemap lhs rhs mode = x.MapCore lhs rhs mode false
    member x.MapWithRemap lhs rhs mode = x.MapCore lhs rhs mode true

    member x.Clear mode = _map <- _map |> Map.remove mode
    member x.ClearAll () = _map <- Map.empty

    /// Main API for adding a key mapping into our storage
    member private x.MapCore (lhs:string) (rhs:string) (mode:KeyRemapMode) allowRemap = 
        if StringUtil.Length lhs <> 1 || StringUtil.Length rhs <= 0 then 
            false
        else
            let lhs = x.ParseKeyBinding lhs
            let rhs = x.ParseKeyBinding rhs
            match lhs,rhs with
            | Some(leftSeq),Some(rightSeq) ->
                let leftKi = leftSeq |> Seq.head
                let value = (rightSeq,allowRemap)
                let modeMap = 
                    match _map |> Map.tryFind mode with
                    | None -> Map.empty
                    | Some(modeMap) -> modeMap
                let modeMap = Map.add leftKi value modeMap
                _map <- Map.add mode modeMap _map
                true
            | _ -> false


    member x.Unmap lhs mode = 
        if StringUtil.Length lhs <> 1 then false
        else
            match x.ParseKeyBinding lhs with
            | Some(leftSeq) ->
                let ki = leftSeq |> Seq.head
                match _map |> Map.tryFind mode with
                | None -> false
                | Some(modeMap) -> 
                    if Map.containsKey ki modeMap then
                        let modeMap = Map.remove ki modeMap
                        _map <- Map.add mode modeMap _map
                        true
                    else
                        false
            | None -> false

    /// Parse out the passed in key bindings.  Returns None in the case of a bad
    /// format on data or a Some KeyInput list on success
    member private x.ParseKeyBinding (data:string) =
        let hasBadData = 
            data 
            |> Seq.filter (fun c -> not (System.Char.IsLetterOrDigit(c)))
            |> SeqUtil.isNotEmpty
        if hasBadData then None
        else data |> Seq.map InputUtil.CharToKeyInput |> Some

    /// Get the key mapping for the passed in data.  Returns a tuple of (KeyInput seq,bool,Set<KeyInput>)
    /// where the bool value is true if there is a recursive mapping.  The Set parameter
    /// tracks the KeyInput values we've already seen in order to detect recursion 
    member private x.GetKeyMappingCore ki mode set = 
        if Set.contains ki set then (Seq.empty, true, set)
        else
            match _map |> Map.tryFind mode with
            | None -> (Seq.empty, false, set)
            | Some(modeMap) ->  
                match modeMap |> Map.tryFind ki with
                | None -> (Seq.empty, false, set)
                | Some(kiSeq,allowRemap) -> 
                    let set = set |> Set.add ki
                    if allowRemap then
                        let mutable anyRecursive = false
                        let mutable set = set
                        let list = new System.Collections.Generic.List<KeyInput>()
                        for mappedKi in kiSeq do
                            let mappedSeq, isRecursive,newSet = x.GetKeyMappingCore mappedKi mode set
                            set <- newSet
                            if isRecursive then
                                anyRecursive <- true
                                list.Add(mappedKi)
                            elif mappedSeq |> Seq.isEmpty then
                                list.Add(mappedKi)
                            else
                                list.AddRange(mappedSeq)

                        (list :> KeyInput seq, anyRecursive, set)

                    else
                        (kiSeq, false, set)

            

    interface IKeyMap with
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.GetKeyMappingResult ki mode = x.GetKeyMappingResult ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

