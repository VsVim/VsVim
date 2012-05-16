#light

namespace Vim

/// Map of a LHS of a mapping to the RHS.  The bool is used to indicate whether or not 
/// the RHS should be remapped as part of an expansion
type internal RemapModeMap = Map<KeyInputSet, KeyMapping>

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

    /// Unmap by considering the expansion
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

    /// Merge the KeyInputSet with the KeyMappingResult 
    member x.MergeSetWithResult (keyInputSet : KeyInputSet) anyMapped (result : KeyMappingResult) =
        match result with 
        | KeyMappingResult.Mapped mappedSet -> KeyInputSetUtil.Combine keyInputSet mappedSet |> KeyMappingResult.Mapped
        | KeyMappingResult.NeedsMoreInput ambiguousSet-> KeyMappingResult.MappedAndNeedsMoreInput (keyInputSet, ambiguousSet)
        | KeyMappingResult.MappedAndNeedsMoreInput (mappedSet, ambiguousSet) -> KeyMappingResult.MappedAndNeedsMoreInput ((KeyInputSetUtil.Combine keyInputSet mappedSet), ambiguousSet)
        | KeyMappingResult.Recursive -> result
        | KeyMappingResult.NoMapping unmappedSet -> 
            let func = if anyMapped then KeyMappingResult.Mapped else KeyMappingResult.NoMapping
            KeyInputSetUtil.Combine keyInputSet unmappedSet |> func

    /// During the processing of mapping input we found a match for the inputKeySet to the 
    /// specified KeyMapping entry.  This needs to be processed
    member x.ProcessMapping (lhs : KeyInputSet) (keyMapping : KeyMapping) depth modeMap = 

        // It's possible that the {lhs} doesn't fully match the {lhs} of the mode mapping
        // for the mapping.  This happens when we have an ambiguous mapping and need a 
        // key stroke unrelated to the mapping to break it (:help map-ambiguous).  
        //
        //  :nmap aa one
        //  :nmap aaa two 
        //
        // The user can then type aak and we will end up with a remaining KeyInputSet of k 
        // here which must still be mapped
        let trailingSet = 
            if lhs.Length > keyMapping.Left.Length then
                lhs.KeyInputs |> ListUtil.skip keyMapping.Left.Length |> KeyInputSetUtil.OfList
            else
                KeyInputSet.Empty

        let mappingResult = 
            if keyMapping.AllowRemap then
                // Need to remap the {rhs} of the match here.  Check for the matching prefix
                // case to prevent infinite remaps (:help recursive_mapping)
                let mappedSet, rhs = 
                    match lhs.FirstKeyInput, keyMapping.Right.FirstKeyInput with
                    | Some leftKey, Some rightKey ->
                        if leftKey = rightKey then
                            let mappedSet = KeyInputSet.OneKeyInput leftKey
                            let rhs = keyMapping.Right.KeyInputs |> List.tail |> KeyInputSetUtil.OfList
                            mappedSet, rhs
                        else
                            KeyInputSet.Empty, keyMapping.Right
                    | _ -> KeyInputSet.Empty, keyMapping.Right

                let mappingResult = x.GetKeyMappingCore rhs (depth + 1) modeMap
                x.MergeSetWithResult mappedSet true mappingResult

            else
                // No remapping case.  All of the {rhs} is simple a part of the output 
                // here 
                KeyMappingResult.Mapped keyMapping.Right

        if trailingSet.Length = 0 then
            mappingResult
        else
            match mappingResult with
            | KeyMappingResult.Mapped keyInputSet -> 
                let trailingResult = x.GetKeyMappingCore trailingSet 0 modeMap
                x.MergeSetWithResult keyInputSet true trailingResult
            | KeyMappingResult.NoMapping keyInputSet ->
                let trailingResult = x.GetKeyMappingCore trailingSet 0 modeMap
                x.MergeSetWithResult keyInputSet false trailingResult
            | KeyMappingResult.MappedAndNeedsMoreInput (mappedSet, ambiguousSet) ->
                let ambiguousSet = KeyInputSetUtil.Combine ambiguousSet trailingSet 
                KeyMappingResult.MappedAndNeedsMoreInput (mappedSet, trailingSet)
            | KeyMappingResult.Recursive -> mappingResult
            | KeyMappingResult.NeedsMoreInput _ -> mappingResult

    /// Get the mapping for the specified lhs in the particular map
    member x.GetKeyMappingCore (lhs : KeyInputSet) depth (modeMap : Map<KeyInputSet, KeyMapping>) =

        if lhs.Length = 0 then
            KeyMappingResult.NoMapping lhs
        elif depth = _globalSettings.MaxMapDepth then
            // If we hit the recursive depth max then break out and return recursive 
            KeyMappingResult.Recursive 
        else
            let prefixMatchArray = 
                modeMap
                |> Seq.filter (fun pair -> pair.Key.StartsWith(lhs))
                |> Array.ofSeq
            match prefixMatchArray with
            | [| |] -> 
                // No matches.  It's possible that there was an ambiguous prefix which this 
                // input just broke.  Need to search for keys which are prefixes to our 
                // input
                let brokenMatchArray = 
                    modeMap
                    |> Seq.filter (fun pair -> lhs.StartsWith(pair.Key))
                    |> Array.ofSeq
                if brokenMatchArray.Length = 1 then
                    x.ProcessMapping lhs brokenMatchArray.[0].Value depth modeMap
                elif lhs.Length > 1 then
                    // If there is more than one item in the {lhs} then we need to attempt to 
                    // map the remainder.
                    let mappedSet = KeyInputSet.OneKeyInput lhs.FirstKeyInput.Value
                    let lhs = lhs.KeyInputs |> ListUtil.skip 1 |> KeyInputSetUtil.OfList
                    let result = x.GetKeyMappingCore lhs 0 modeMap
                    x.MergeSetWithResult mappedSet false result
                else
                    KeyMappingResult.NoMapping lhs
            | [| pair |] -> 
                if pair.Key = lhs then
                    x.ProcessMapping lhs pair.Value depth modeMap 
                else
                    KeyMappingResult.NeedsMoreInput lhs
            | _ -> KeyMappingResult.NeedsMoreInput lhs

    /// Get the key mapping for the passed in data.  Returns a KeyMappingResult representing the 
    /// mapping
    member x.GetKeyMapping (keyInputSet : KeyInputSet) mode =
        let modeMap = x.GetRemapModeMap mode
        x.GetKeyMappingCore keyInputSet 0 modeMap

    interface IKeyMap with
        member x.GetKeyMappingsForMode mode = x.GetKeyMappingsForMode mode 
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.UnmapByMapping rhs mode = x.UnmapByMapping rhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

