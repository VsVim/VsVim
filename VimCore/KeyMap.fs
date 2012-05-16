#light

namespace Vim

/// Map of a LHS of a mapping to the RHS.  The bool is used to indicate whether or not 
/// the RHS should be remapped as part of an expansion
type RemapModeMap = Map<KeyInputSet, KeyMapping>

/// The type responsible for actually converting the mapping.  This is naturally a recursive
/// process but is done here iteratively.  There are many Vim scenarios which call for 
/// extremely deep mappings (several thousands).  Much too deep for the natural head recursive
/// method
///
/// I coded up a continuation passing style version but I found the logic was very hard to 
/// follow and very easy to break with a simple error.  This process is already fairly
/// complex and I didn't want to make it more so.  Eventually I went with the iterative 
/// approach because it's easy to follow and meets the specs
type Mapper
    (
        _keyInputSet : KeyInputSet,
        _modeMap : RemapModeMap,
        _globalSettings : IVimGlobalSettings
    ) =

    let _mappedList = System.Collections.Generic.List<KeyInput>()

    /// During the processing of mapping input we found a match for the inputKeySet to the 
    /// specified KeyMapping entry.  This needs to be processed
    member x.ProcessMapping (lhs : KeyInputSet) (keyMapping : KeyMapping) = 

        let toMapList = 
            if keyMapping.AllowRemap then
                // Need to remap the {rhs} of the match here.  Check for the matching prefix
                // case to prevent infinite remaps (:help recursive_mapping)
                let rhs = 
                    match lhs.FirstKeyInput, keyMapping.Right.FirstKeyInput with
                    | Some leftKey, Some rightKey ->
                        if leftKey = rightKey then
                            _mappedList.Add(leftKey)
                            keyMapping.Right.KeyInputs |> List.tail |> KeyInputSetUtil.OfList
                        else
                            keyMapping.Right
                    | _ -> keyMapping.Right

                /// Still need to map the {rhs}
                rhs.KeyInputs

            else
                // No remapping case.  All of the {rhs} is simple a part of the output 
                // here 
                _mappedList.AddRange(keyMapping.Right.KeyInputs)

                List.empty

        // It's possible that the {lhs} doesn't fully match the {lhs} of the mode mapping
        // for the mapping.  This happens when we have an ambiguous mapping and need a 
        // key stroke unrelated to the mapping to break it (:help map-ambiguous).  
        //
        //  :nmap aa one
        //  :nmap aaa two 
        //
        // The user can then type aak and we will end up with a remaining KeyInputSet of k 
        // here which must still be mapped
        let toMapList = 
            if lhs.Length > keyMapping.Left.Length then
                let list = lhs.KeyInputs |> ListUtil.skip keyMapping.Left.Length 
                List.append toMapList list
            else
                toMapList

        toMapList

    /// Get the mapping for the specified lhs in the particular map
    member x.GetKeyMapping()  =

        let failed = ref false
        let failedResult = ref KeyMappingResult.Recursive
        let depth = ref 0
        let toMapList = ref _keyInputSet.KeyInputs

        let needsMoreInput () = 
            if _mappedList.Count = 0 then
                failedResult := KeyMappingResult.NeedsMoreInput _keyInputSet
                failed := true
            else
                // At least one mapped so that's good enough for now
                _mappedList.AddRange toMapList.Value
                toMapList := List.empty

        let processMapping lhs keyMapping =
            depth := depth.Value + 1
            if depth.Value = _globalSettings.MaxMapDepth then
                // Exceeded the maximum depth of recursive mappings.  Break out of this with 
                // Recursive
                failedResult := KeyMappingResult.Recursive
                failed := true
            else
                toMapList := x.ProcessMapping lhs keyMapping

        while toMapList.Value.Length > 0 && not failed.Value do

            let lhs = toMapList.Value |> KeyInputSetUtil.OfSeq
            let prefixMatchArray = 
                _modeMap
                |> Seq.filter (fun pair -> pair.Key.StartsWith(lhs))
                |> Array.ofSeq
            match prefixMatchArray with
            | [| |] -> 
                // No matches.  It's possible that there was an ambiguous prefix which this 
                // input just broke.  Need to search for keys which are prefixes to our 
                // input
                let brokenMatchArray = 
                    _modeMap
                    |> Seq.filter (fun pair -> lhs.StartsWith(pair.Key))
                    |> Array.ofSeq
                if brokenMatchArray.Length = 1 then
                    processMapping lhs brokenMatchArray.[0].Value
                else
                    // No more mappings for this prefix so we are done 
                    _mappedList.AddRange(toMapList.Value)
                    toMapList := List.empty
            | [| pair |] -> 
                if pair.Key = lhs then
                    processMapping lhs pair.Value
                else
                    needsMoreInput()
            | _ -> needsMoreInput()

        if failed.Value then
            failedResult.Value
        else
            _mappedList |> KeyInputSetUtil.OfSeq |> KeyMappingResult.Mapped

type internal KeyMap(_globalSettings : IVimGlobalSettings) =

    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty

    member x.MapWithNoRemap lhs rhs mode = x.MapCore lhs rhs mode false
    member x.MapWithRemap lhs rhs mode = x.MapCore lhs rhs mode true

    member x.Clear mode = _map <- _map |> Map.remove mode
    member x.ClearAll () = _map <- Map.empty

    /// Get a RemapModeMap for the given KeyRemapMode
    member x.GetRemapModeMap mode = 
        match Map.tryFind mode _map with
        | None -> Map.empty
        | Some(map) -> map

    /// Get all of the mappings for the given KeyRemapMode
    member x.GetKeyMappingsForMode mode : KeyMapping list = 
        match Map.tryFind mode _map with
        | None -> List.empty
        | Some map -> map |> Map.toSeq |> Seq.map snd |> List.ofSeq

    /// Main API for adding a key mapping into our storage
    member x.MapCore (lhs : string) (rhs : string) (mode : KeyRemapMode) allowRemap = 
        if StringUtil.isNullOrEmpty rhs then
            false
        else
            let key = KeyNotationUtil.TryStringToKeyInputSet lhs
            let rhs = KeyNotationUtil.TryStringToKeyInputSet rhs
            match key, rhs with
            | Some left, Some right ->
                let keyMapping = {
                    Left = left
                    Right = right
                    AllowRemap = allowRemap
                }
                let modeMap = x.GetRemapModeMap mode
                let modeMap = Map.add left keyMapping modeMap
                _map <- Map.add mode modeMap _map
                true
            | _ -> 
                // Need both a valid LHS and RHS to create a mapping
                false

    member x.Unmap lhs mode = 
        match KeyNotationUtil.TryStringToKeyInputSet lhs with
        | None -> false
        | Some key ->
            let modeMap = x.GetRemapModeMap mode
            if Map.containsKey key modeMap then
                let modeMap = Map.remove key modeMap
                _map <- Map.add mode modeMap _map
                true
            else
                false

    member x.UnmapByMapping rhs mode = 
        match KeyNotationUtil.TryStringToKeyInputSet rhs with
        | None -> false
        | Some right ->
            let modeMap = x.GetRemapModeMap mode
            let key = modeMap |> Map.tryFindKey (fun _ keyMapping -> keyMapping.Right = right)
            match key with
            | None -> false
            | Some key ->
                let modeMap = Map.remove key modeMap
                _map <- Map.add mode modeMap _map
                true

    /// Get the key mapping for the passed in data.  Returns a KeyMappingResult representing the 
    /// mapping
    member x.GetKeyMapping (keyInputSet : KeyInputSet) mode =
        let modeMap = x.GetRemapModeMap mode
        let mapper = Mapper(keyInputSet, modeMap, _globalSettings)
        mapper.GetKeyMapping()

    interface IKeyMap with
        member x.GetKeyMappingsForMode mode = x.GetKeyMappingsForMode mode 
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.UnmapByMapping rhs mode = x.UnmapByMapping rhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

