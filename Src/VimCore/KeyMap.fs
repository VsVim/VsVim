#light

namespace Vim
open System
open Vim.Interpreter

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
        _globalSettings : IVimGlobalSettings,
        _isZeroMappingEnabled : bool
    ) =

    /// During the processing of mapping input we found a match for the inputKeySet to the 
    /// specified KeyMapping entry.  Return the KeyInputSet which this definitively mapped
    /// to (and requires no remapping) and the set of keys which still must be considered
    /// for mapping
    member x.ProcessMapping (lhs : KeyInputSet) (keyMapping : KeyMapping) = 

        let mappedSet, remainingSet = 
            if keyMapping.AllowRemap then
                // Need to remap the {rhs} of the match here.  Check for the matching prefix
                // case to prevent infinite remaps (:help recursive_mapping)
                match lhs.FirstKeyInput, keyMapping.Right.FirstKeyInput with
                | Some leftKey, Some rightKey ->
                    if leftKey = rightKey then
                        let mappedSet = KeyInputSet.OneKeyInput leftKey
                        let remainingSet = keyMapping.Right.KeyInputs |> List.tail |> KeyInputSetUtil.OfList
                        mappedSet, remainingSet
                    else
                        KeyInputSet.Empty, keyMapping.Right
                | _ -> KeyInputSet.Empty, keyMapping.Right

            else
                // No remapping case.  All of the {rhs} is simple a part of the output 
                // here and require no further mapping
                keyMapping.Right, KeyInputSet.Empty

        // It's possible that the {lhs} doesn't fully match the {lhs} of the mode mapping
        // for the mapping.  This happens when we have an ambiguous mapping and need a 
        // key stroke unrelated to the mapping to break it (:help map-ambiguous).  
        //
        //  :nmap aa one
        //  :nmap aaa two 
        //
        // The user can then type 'aak' and we will end up with a remaining KeyInputSet of k 
        // here which must still be mapped
        let remainingSet = 
            if lhs.Length > keyMapping.Left.Length then
                let extraSet = 
                    lhs.KeyInputs 
                    |> Seq.ofList
                    |> Seq.skip keyMapping.Left.Length
                    |> KeyInputSetUtil.OfSeq
                KeyInputSetUtil.Combine remainingSet extraSet
            else
                remainingSet

        mappedSet, remainingSet

    static member IsFirstKeyInputZero (keyInputSet : KeyInputSet) =
        match keyInputSet.FirstKeyInput with
        | Some keyInput -> keyInput = KeyInputUtil.CharToKeyInput '0' 
        | _ -> false

    /// Get the mapping for the specified lhs in the particular map
    member x.GetKeyMapping()  =

        let isDone = ref false
        let result = ref KeyMappingResult.Recursive
        let depth = ref 0
        let mappedSet = ref KeyInputSet.Empty
        let remainingSet = ref _keyInputSet

        let needsMoreInput () = 
            result := KeyMappingResult.NeedsMoreInput _keyInputSet
            isDone := true

        // Called when there is a successful mapping of the data.  Once we map anything 
        // definitively then we are done.  The rest of the data (if any) shouldn't be processed
        // until the calling code has time to process the definitively mapped set of 
        // keys.  Doing so could change the key mapping mode and hence change how we map 
        // the remaining keys
        let successfulMap (mappedKeyInputSet : KeyInputSet) (remainingKeyInputSet : KeyInputSet) = 
            if remainingKeyInputSet.Length = 0 then
                result := KeyMappingResult.Mapped mappedKeyInputSet
            else
                result := KeyMappingResult.PartiallyMapped (mappedKeyInputSet, remainingKeyInputSet)

            isDone := true

        let processMapping lhs keyMapping =
            depth := depth.Value + 1
            if depth.Value = _globalSettings.MaxMapDepth then
                // Exceeded the maximum depth of recursive mappings.  Break out of this with 
                // Recursive
                result := KeyMappingResult.Recursive
                isDone := true
            elif not _isZeroMappingEnabled && Mapper.IsFirstKeyInputZero lhs then
                // 0 mapping is disabled and we have a 0 input so we are done
                let mappedKeyInputSet = KeyInputUtil.CharToKeyInput '0' |> KeyInputSet.OneKeyInput
                let remainingKeyInputSet = lhs.Rest |> KeyInputSetUtil.OfList
                successfulMap mappedKeyInputSet remainingKeyInputSet
            else
                let mapped, remaining = x.ProcessMapping lhs keyMapping
                mappedSet := mapped
                remainingSet := remaining

                if mappedSet.Value.Length > 0 then
                    successfulMap mapped remaining

        while not isDone.Value do
            Contract.Assert(mappedSet.Value.Length = 0)

            let lhs = remainingSet.Value
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
                elif lhs.Length = 1 then
                    // No mappings for the lhs so we are done
                    result := KeyMappingResult.Mapped lhs
                    isDone := true
                else
                    // First character can't be mapped but the rest is still eligible for 
                    // mapping after that character is complete 
                    let mapped = KeyInputSet.OneKeyInput lhs.FirstKeyInput.Value
                    let remaining = lhs.Rest |> KeyInputSetUtil.OfList
                    result := KeyMappingResult.PartiallyMapped (mapped, remaining)
                    isDone := true

            | [| pair |] -> 
                if pair.Key = lhs then
                    processMapping lhs pair.Value
                else
                    needsMoreInput()
            | _ -> 
                // More than one match so we need more input to disambiguate 
                needsMoreInput()

        result.Value

type internal KeyMap
    (
        _globalSettings : IVimGlobalSettings,
        _variableMap : VariableMap
    ) =

    let mutable _map : Map<KeyRemapMode, RemapModeMap> = Map.empty
    let mutable _isZeroMappingEnabled = true

    member x.IsZeroMappingEnabled 
        with get() = _isZeroMappingEnabled
        and set value = _isZeroMappingEnabled <- value

    member x.MapWithNoRemap lhs rhs mode = x.MapCore lhs rhs mode false
    member x.MapWithRemap lhs rhs mode = x.MapCore lhs rhs mode true

    member x.Clear mode = _map <- _map |> Map.remove mode
    member x.ClearAll () = _map <- Map.empty

    /// Get a RemapModeMap for the given KeyRemapMode
    member x.GetRemapModeMap mode = 
        match Map.tryFind mode _map with
        | None -> Map.empty
        | Some map -> map

    /// Get all of the mappings for the given KeyRemapMode
    member x.GetKeyMappingsForMode mode : KeyMapping list = 
        match Map.tryFind mode _map with
        | None -> List.empty
        | Some map -> map |> Map.toSeq |> Seq.map snd |> List.ofSeq

    /// Main API for adding a key mapping into our storage
    member x.MapCore (lhs : string) (rhs : string) (mode : KeyRemapMode) allowRemap = 

        // Replace the <Leader> value with the appropriate replacement string
        let replaceLeader (str : string) =
            if str.IndexOf("<leader>", StringComparison.OrdinalIgnoreCase) >= 0 then
                let replace =
                    let found, value = _variableMap.TryGetValue "mapleader"
                    if found then 
                        value.StringValue
                    else
                        "\\"
                StringUtil.replaceNoCase str "<leader>" replace
            else
                str

        if StringUtil.isNullOrEmpty rhs then
            false
        else
            let lhs = replaceLeader lhs
            let rhs = replaceLeader rhs
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
        let mapper = Mapper(keyInputSet, modeMap, _globalSettings, _isZeroMappingEnabled)
        mapper.GetKeyMapping()

    interface IKeyMap with
        member x.IsZeroMappingEnabled 
            with get() = x.IsZeroMappingEnabled
            and set value = x.IsZeroMappingEnabled <- value
        member x.GetKeyMappingsForMode mode = x.GetKeyMappingsForMode mode 
        member x.GetKeyMapping ki mode = x.GetKeyMapping ki mode
        member x.MapWithNoRemap lhs rhs mode = x.MapWithNoRemap lhs rhs mode
        member x.MapWithRemap lhs rhs mode = x.MapWithRemap lhs rhs mode
        member x.Unmap lhs mode = x.Unmap lhs mode
        member x.UnmapByMapping rhs mode = x.UnmapByMapping rhs mode
        member x.Clear mode = x.Clear mode
        member x.ClearAll () = x.ClearAll()

