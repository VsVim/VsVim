#light

namespace Vim

type internal RemapModeMap = Map<KeyInputSet, (KeyInputSet * bool)>

type internal KeyMap() =
    
    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    member x.MapWithNoRemap lhs rhs mode = x.MapCore lhs rhs mode false
    member x.MapWithRemap lhs rhs mode = x.MapCore lhs rhs mode true

    member x.Clear mode = _map <- _map |> Map.remove mode
    member x.ClearAll () = _map <- Map.empty

    member x.GetRemapModeMap mode = 
        match Map.tryFind mode _map with
        | None -> Map.empty
        | Some(map) -> map

    /// Main API for adding a key mapping into our storage
    member x.MapCore (lhs:string) (rhs:string) (mode:KeyRemapMode) allowRemap = 
        if StringUtil.isNullOrEmpty rhs then
            false
        else
            let key = KeyNotationUtil.TryStringToKeyInputSet lhs
            let rhs = KeyNotationUtil.TryStringToKeyInputSet rhs
            match key,rhs with
            | Some(key),Some(rightList) ->
                let value = (rightList,allowRemap)
                let modeMap = x.GetRemapModeMap mode
                let modeMap = Map.add key value modeMap
                _map <- Map.add mode modeMap _map
                true
            | _ -> false

    member x.Unmap lhs mode = 
        match KeyNotationUtil.TryStringToKeyInputSet lhs with
        | None -> false
        | Some(key) ->
            let modeMap = x.GetRemapModeMap mode
            if Map.containsKey key modeMap then
                let modeMap = Map.remove key modeMap
                _map <- Map.add mode modeMap _map
                true
            else
                false

    /// Get the key mapping for the passed in data.  Returns a KeyMappingResult represeting the 
    /// mapping
    member x.GetKeyMapping keyInputSet mode =
        let modeMap = x.GetRemapModeMap mode

        let rec inner key set : (KeyMappingResult * Set<KeyInputSet> )=
            if Set.contains key set then (RecursiveMapping key,set)
            else
                match modeMap |> Map.tryFind key with
                | None -> 
                    // Determine if there is a prefix match for an existing key 
                    let matchesPrefix = 
                        modeMap 
                        |> MapUtil.keys
                        |> Seq.filter (fun fullKey -> fullKey.StartsWith(key) )
                        |> SeqUtil.isNotEmpty
                    if matchesPrefix then MappingNeedsMoreInput,set
                    else NoMapping,set
                | Some(mappedKeyInputs,allowRemap) -> 
                    let set = set |> Set.add key
                    if not allowRemap then (Mapped mappedKeyInputs), set
                    else  
                        
                        // Time for a recursive mapping attempt
                        let mutable anyRecursive = false
                        let mutable set = set
                        let list = new System.Collections.Generic.List<KeyInput>()
                        for mappedKi in mappedKeyInputs.KeyInputs do
                            let result,newSet = inner (OneKeyInput mappedKi) set
                            set <- newSet

                            match result with
                            | NoMapping -> list.Add(mappedKi)
                            | Mapped(keyInputSet) -> list.AddRange(keyInputSet.KeyInputs)
                            | RecursiveMapping(_) ->
                                list.Add(mappedKi)
                                anyRecursive <- true
                            | MappingNeedsMoreInput-> list.Add(mappedKi)

                        let keyInputSet = list |> KeyInputSetUtil.ofSeq 
                        if anyRecursive then (RecursiveMapping keyInputSet, set)
                        else (Mapped keyInputSet, set)
    
        let res,_ = inner keyInputSet Set.empty
        res
    
    interface IKeyMap with
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

