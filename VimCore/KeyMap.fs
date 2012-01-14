#light

namespace Vim

/// Map of a LHS of a mapping to the RHS.  The bool is used to indicate whether or not 
/// the RHS should be remapped as part of an expansion
type internal RemapModeMap = Map<KeyInputSet, KeyMapping>

type internal KeyMap() =
    
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

    /// Get the key mapping for the passed in data.  Returns a KeyMappingResult representing the 
    /// mapping
    member x.GetKeyMapping (keyInputSet : KeyInputSet) mode =
        let modeMap = x.GetRemapModeMap mode

        let rec inner (current : KeyInputSet) (remaining : KeyInputSet) (result : KeyInputSet) didMapAny sourceMapping seenSet =
            Contract.Assert(current.Length >= 0)

            let processNext (remaining : KeyInputSet) (result : KeyInputSet) didMapAny = 
                match remaining.KeyInputs with
                | [] ->
                    if didMapAny then
                        KeyMappingResult.Mapped result
                    else
                        KeyMappingResult.NoMapping
                | head :: tail -> 
                    let current = KeyInputSet.OneKeyInput head
                    let remaining = KeyInputSetUtil.OfList tail
                    inner current remaining result didMapAny sourceMapping seenSet

            // We were able to map current to a set of mapped keys which request remapping.  Process that remapping
            // now 
            let processRemap current (mapped : KeyInputSet) (result : KeyInputSet) seenSet =
                if mapped.Length = 0 then
                    // This indicates an error.  The mapped values should always have at least length 1
                    KeyMappingResult.NoMapping
                else
                    let newSourceMapping = Some current
                    let newSeenSet = Set.add current seenSet
                    let newRemaining = mapped.Rest |> KeyInputSetUtil.OfList
                    let current = mapped.FirstKeyInput |> Option.get |> KeyInputSet.OneKeyInput
                    match inner current newRemaining KeyInputSet.Empty false newSourceMapping newSeenSet with
                    | KeyMappingResult.Mapped mapped ->
                        let result = KeyInputSetUtil.Combine result mapped
                        processNext remaining result true
                    | KeyMappingResult.NoMapping ->
                        let result = KeyInputSetUtil.Combine result mapped
                        processNext remaining result true
                    | KeyMappingResult.Recursive ->
                        KeyMappingResult.Recursive
                    | KeyMappingResult.NeedsMoreInput ->
                        KeyMappingResult.NeedsMoreInput

            let doMap () = 
                match modeMap |> Map.tryFind current with
                | None -> 
                    // The current item considered for mapping doesn't match an existing key.  It could still 
                    // be the prefix of a key though.  
                    let matchesPrefix = 
                        modeMap 
                        |> MapUtil.keys
                        |> Seq.filter (fun fullKey -> fullKey.StartsWith(current))
                        |> SeqUtil.isNotEmpty
                    if matchesPrefix then 
                        // If there is a prefix match then we need to consider the next key in the LHS and 
                        // add that in 
                        match remaining.KeyInputs with
                        | [] -> 
                            // More input is needed to resolve the ambiguity and none exists.
                            KeyMappingResult.NeedsMoreInput
                        | head :: tail ->
                            let current = current.Add head
                            let remaining = KeyInputSetUtil.OfList tail
                            inner current remaining result didMapAny sourceMapping seenSet
                    elif current.Length > 1 then
                        // This occurs when values of 'current - 1' was a prefix match and we had to consider
                        // the next key to disambiguate the match.  We've now determined it doesn't match 
                        // anything.  So add in the first item of current and re-process the rest of items
                        let result = result.Add (current.FirstKeyInput |> Option.get)
                        let remaining = current.Rest @ remaining.KeyInputs |> KeyInputSetUtil.OfList
                        processNext remaining result didMapAny
                    else
                        // Single KeyInput didn't match.  Time to move along
                        let result = result.Add (current.FirstKeyInput |> Option.get)
                        processNext remaining result didMapAny
                | Some keyMapping ->
                    if keyMapping.AllowRemap then
                        processRemap current keyMapping.Right result seenSet
                    else
                        let result = KeyInputSetUtil.Combine result keyMapping.Right
                        processNext remaining result true

            let currentEqualMapping = 
                match sourceMapping with
                | None -> false
                | Some sourceMapping -> sourceMapping = current
            if currentEqualMapping then
                // If the current item we're currently mapping then don't map it again.  In short
                // when considering the mapping ':map j gj' we don't want it to be recursive
                let result = KeyInputSetUtil.Combine result current
                processNext remaining result didMapAny
            elif Set.contains current seenSet then
                // This is a recursive mapping.  
                KeyMappingResult.Recursive
            else
                doMap()

        match keyInputSet.FirstKeyInput with
        | None -> 
            KeyMappingResult.NoMapping
        | Some head ->
            let current = KeyInputSet.OneKeyInput head
            let remaining = KeyInputSetUtil.OfList keyInputSet.Rest
            inner current remaining KeyInputSet.Empty false None Set.empty
    
    interface IKeyMap with
        member x.GetKeyMappingsForMode mode = x.GetKeyMappingsForMode mode 
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.UnmapByMapping rhs mode = x.UnmapByMapping rhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

