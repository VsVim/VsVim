﻿#light

namespace Vim
open System
open System.Collections.Generic
open System.Diagnostics
open Vim.Interpreter
open CollectionExtensions
open Microsoft.VisualStudio.Text

type internal MappingMap<'TMode, 'TData when 'TMode : equality>(_modeList: 'TMode list) =

    let _map = Dictionary<'TMode, Dictionary<KeyInputSet, 'TData>>(capacity = _modeList.Length)

    do
        for mode in _modeList do
            _map.[mode] <- Dictionary<KeyInputSet, 'TData>()

    member x.Mappings: 'TData seq = 
        seq {
            for kvp in _map.Values do
                yield! kvp.Values
        }

    member x.Add(key, mode, data) =
        let map = _map.[mode]
        map.[key] <- data

    member x.Remove(key, mode) =
        let map = _map.[mode] 
        map.Remove key

    member x.Get(key, mode) =
        let map = _map.[mode]
        map.TryGetValueEx key

    member x.Get(mode) = _map.[mode].Values :> 'TData seq

    member x.Clear(mode) =
        let map = _map.[mode]
        map.Clear()

    member x.Clear() = 
        for map in _map.Values do
            map.Clear()

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
        _keyInputSet: KeyInputSet,
        _keyMappings: KeyMapping seq,
        _globalSettings: IVimGlobalSettings,
        _isZeroMappingEnabled: bool,
        _overrideAllowRemap: bool option
    ) =

    /// During the processing of mapping input we found a match for the inputKeySet to the 
    /// specified KeyMapping entry.  Return the KeyInputSet which this definitively mapped
    /// to (and requires no remapping) and the set of keys which still must be considered
    /// for mapping
    member x.ProcessMapping (lhs: KeyInputSet) (keyMapping: KeyMapping) = 

        let mappedSet, remainingSet = 
            let allowRemap = 
                match _overrideAllowRemap with
                | Some b -> b
                | None -> keyMapping.AllowRemap
            if allowRemap then
                // Need to remap the {rhs} of the match here.  Check for the matching prefix
                // case to prevent infinite remaps (:help recursive_mapping)
                match lhs.FirstKeyInput, keyMapping.Right.FirstKeyInput with
                | Some leftKey, Some rightKey ->
                    if leftKey = rightKey then
                        let mappedSet = KeyInputSet(leftKey)
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

    static member IsFirstKeyInputZero (keyInputSet: KeyInputSet) =
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
        let successfulMap (mappedKeyInputSet: KeyInputSet) (remainingKeyInputSet: KeyInputSet) = 
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
                let mappedKeyInputSet = KeyInputUtil.CharToKeyInput '0' |> KeyInputSetUtil.Single
                let remainingKeyInputSet = lhs.Rest
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
                _keyMappings
                |> Seq.filter (fun keyMapping -> keyMapping.Left.StartsWith(lhs))
                |> Array.ofSeq
            match prefixMatchArray with
            | [| |] -> 
                // No matches.  It's possible that there was an ambiguous prefix which this 
                // input just broke.  Need to search for keys which are prefixes to our 
                // input
                let brokenMatchArray = 
                    _keyMappings
                    |> Seq.filter (fun keyMapping -> lhs.StartsWith(keyMapping.Left))
                    |> Array.ofSeq
                if brokenMatchArray.Length = 1 then
                    processMapping lhs brokenMatchArray.[0]
                elif lhs.Length = 1 then
                    // No mappings for the lhs so we are done
                    result := KeyMappingResult.Unmapped lhs
                    isDone := true
                else
                    // First character can't be mapped but the rest is still eligible for 
                    // mapping after that character is complete 
                    let mapped = KeyInputSet(lhs.FirstKeyInput.Value)
                    let remaining = lhs.Rest
                    result := KeyMappingResult.PartiallyMapped (mapped, remaining)
                    isDone := true

            | [| keyMapping |] -> 
                if keyMapping.Left = lhs then
                    processMapping lhs keyMapping
                else
                    needsMoreInput()
            | _ -> 
                // More than one match so we need more input to disambiguate 
                needsMoreInput()

        result.Value

type internal GlobalKeyMap(variableMap: VariableMap) =

    let _variableMap = variableMap
    let _map = MappingMap<KeyRemapMode, KeyMapping>(KeyRemapMode.All)

    static member ParseKeyNotation(notation: string, variableMap: VariableMap) =
        let notation = 
            if notation.IndexOf("<leader>", StringComparison.OrdinalIgnoreCase) >= 0 then
                let replace =
                    let found, value = variableMap.TryGetValue "mapleader"
                    if found then 
                        value.StringValue
                    else
                        "\\"
                StringUtil.ReplaceNoCase notation "<leader>" replace
            else
                notation
        KeyNotationUtil.TryStringToKeyInputSet notation

    interface IVimGlobalKeyMap with
        member x.KeyMappings = _map.Mappings
        member x.GetKeyMapping(lhs, mode) = _map.Get(lhs, mode)
        member x.GetKeyMappings(mode) = _map.Get(mode)
        member x.AddKeyMapping(lhs, rhs, allowRemap, mode) = _map.Add(lhs, mode, KeyMapping(lhs, rhs, allowRemap, mode))
        member x.RemoveKeyMapping(lhs, mode) = _map.Remove(lhs, mode)
        member x.ClearKeyMappings(mode) = _map.Clear(mode)
        member x.ClearKeyMappings() = _map.Clear()
        member x.ParseKeyNotation(notation) = GlobalKeyMap.ParseKeyNotation(notation, _variableMap)

type internal LocalKeyMap
    (
        globalKeyMap: IVimGlobalKeyMap,
        globalSettings: IVimGlobalSettings,
        variableMap: VariableMap
    ) =

    let _globalKeyMap = globalKeyMap
    let _globalSettings = globalSettings
    let _variableMap = variableMap
    let _map = MappingMap<KeyRemapMode, KeyMapping>(KeyRemapMode.All)
    let mutable _isZeroMappingEnabled = true

    member x.IsZeroMappingEnabled 
        with get() = _isZeroMappingEnabled
        and set value = _isZeroMappingEnabled <- value

    member x.GetKeyMapping(keyInputSet, mode, includeGlobal) =
        match _map.Get(keyInputSet, mode) with 
        | Some r -> Some r
        | None when includeGlobal -> _globalKeyMap.GetKeyMapping(keyInputSet, mode)
        | None -> None

    member x.GetKeyMappings(mode, includeGlobal) =
        let local = _map.Get(mode)
        if includeGlobal then Seq.append (_globalKeyMap.GetKeyMappings(mode)) local
        else local

    /// Map the passed in KeyInputSet and return the result for the mapping
    member x.Map (keyInputSet: KeyInputSet) allowRemap mode =
        // The set of key mappings to consider are the local ones and the global ones when they 
        // don't conflict with a local one.
        let keyMappings = 
            let localKeyMappings = _map.Get(mode)
            let globalKeyMappings = _globalKeyMap.GetKeyMappings(mode)
            if Seq.isEmpty localKeyMappings then
                globalKeyMappings
            else
                let newGlobalKeyMappings = globalKeyMappings |> Seq.where (fun g -> Option.isNone (_map.Get(g.Left, mode)))
                Seq.append newGlobalKeyMappings localKeyMappings

        let mapper = Mapper(keyInputSet, keyMappings, _globalSettings, _isZeroMappingEnabled, allowRemap)
        mapper.GetKeyMapping()

    interface IVimLocalKeyMap with
        member x.GlobalKeyMap = _globalKeyMap
        member x.IsZeroMappingEnabled 
            with get() = x.IsZeroMappingEnabled
            and set value = x.IsZeroMappingEnabled <- value
        member x.KeyMappings = _map.Mappings
        member x.GetKeyMapping(lhs, mode) = _map.Get(lhs, mode)
        member x.GetKeyMapping(lhs, mode, includeGlobal) = x.GetKeyMapping(lhs, mode, includeGlobal)
        member x.GetKeyMappings(mode) = _map.Get(mode)
        member x.GetKeyMappings(mode, includeGlobal) = x.GetKeyMappings(mode, includeGlobal)
        member x.AddKeyMapping(lhs, rhs, allowRemap, mode) = _map.Add(lhs, mode, KeyMapping(lhs, rhs, allowRemap, mode))
        member x.RemoveKeyMapping(lhs, mode) = _map.Remove(lhs, mode)
        member x.ClearKeyMappings(mode) = _map.Clear(mode)
        member x.ClearKeyMappings() = _map.Clear()
        member x.Map(keyInputSet, mode) = x.Map keyInputSet None mode
        member x.Map(keyInputSet, allowRemap, mode) = x.Map keyInputSet (Some allowRemap) mode
        member x.ParseKeyNotation(notation) = GlobalKeyMap.ParseKeyNotation(notation, _variableMap)

type internal GlobalAbbreviationMap() =

    let _map = MappingMap<AbbreviationMode, Abbreviation>(AbbreviationMode.All)

    member x.Map = _map

    interface IVimGlobalAbbreviationMap with
        member x.Abbreviations = x.Map.Mappings
        member x.AddAbbreviation(lhs, rhs, allowRemap, mode) = x.Map.Add(lhs, mode, (Abbreviation(lhs, rhs, allowRemap, mode)))
        member x.GetAbbreviation(lhs, mode) = x.Map.Get(lhs, mode)
        member x.GetAbbreviations(mode) = x.Map.Get(mode)
        member x.RemoveAbbreviation(lhs, mode) = x.Map.Remove(lhs, mode)
        member x.ClearAbbreviations(mode) = x.Map.Clear(mode)
        member x.ClearAbbreviations() = x.Map.Clear()

type internal LocalAbbreviationMap
    (
        keyMap: IVimLocalKeyMap,
        globalAbbreviationMap: IVimGlobalAbbreviationMap,
        wordUtil : WordUtil
    ) = 

    let _keyMap = keyMap
    let _globalAbbreviationMap = globalAbbreviationMap
    let _wordUtil = wordUtil
    let _map = MappingMap<AbbreviationMode, Abbreviation>(AbbreviationMode.All)

    member x.Map = _map
    member x.IsKeywordChar c = _wordUtil.IsKeywordChar c
    member x.IsNonKeywordChar c = not (_wordUtil.IsKeywordChar c)

    member x.GetAbbreviation(lhs, mode, includeGlobal) = 
        match _map.Get(lhs, mode) with
        | Some rhs -> Some rhs
        | None when includeGlobal -> _globalAbbreviationMap.GetAbbreviation(lhs, mode)
        | None -> None

    member x.GetAbbreviations(mode, includeGlobal) =
        let local = _map.Get(mode)
        if includeGlobal then Seq.append (_globalAbbreviationMap.GetAbbreviations(mode)) local
        else local

    member x.Parse text = 
        if StringUtil.IsNullOrEmpty text || Seq.exists CharUtil.IsWhiteSpace text then
            None
        elif Seq.forall x.IsKeywordChar text then
            Some AbbreviationKind.FullId
        else
            let isLastKeyword = x.IsKeywordChar text.[text.Length - 1]
            let rest = Seq.take (text.Length - 1) text
            if isLastKeyword && Seq.forall x.IsNonKeywordChar rest then
                Some AbbreviationKind.EndId
            elif not isLastKeyword then
                Some AbbreviationKind.NonId
            else
                None

    member x.Abbreviate (text: string) (triggerKeyInput: KeyInput) mode =

        // The replacement portion of an abbreviation is potentially subject to key remapping. 
        let remap (abbreviation: Abbreviation) =
            if abbreviation.AllowRemap then
                let remapMode = 
                    match mode with
                    | AbbreviationMode.Insert -> KeyRemapMode.Insert
                    | AbbreviationMode.Command -> KeyRemapMode.Command

                let result = _keyMap.Map(abbreviation.Replacement, allowRemap = false, mode = remapMode)
                let result =
                    match result with
                    | KeyMappingResult.NeedsMoreInput _ -> 
                        // An undocumented behavior is that when a replacement has an incomplete key mapping then
                        // the trigger key can be used to complete the mapping. This strangely though does not 
                        // consume the trigger key, it is just used to complete a mapping and the trigger key is 
                        // processed as normal
                        let newResult = _keyMap.Map((abbreviation.Replacement.Add triggerKeyInput), allowRemap = false, mode = remapMode)
                        match newResult with
                        | KeyMappingResult.Mapped _ -> newResult
                        | _ -> result
                    | _ -> result
                let replacement = 
                    match result with 
                    | KeyMappingResult.NeedsMoreInput _ -> abbreviation.Replacement
                    | KeyMappingResult.Mapped keyInputSet -> keyInputSet
                    | KeyMappingResult.PartiallyMapped (l, r) -> l.AddRange r
                    | KeyMappingResult.Recursive _ -> abbreviation.Replacement
                    | KeyMappingResult.Unmapped _ -> abbreviation.Replacement
                (replacement, Some result)
            else 
                (abbreviation.Replacement, None)

        let getFullId() = 
            Debug.Assert(text.Length > 0)
            let mutable index = text.Length
            while index > 0 && x.IsKeywordChar text.[index - 1] do
                index <- index - 1
            let fullIdText = text.Substring(index)
            match fullIdText.Length with
            | 0 -> None
            | 1 ->
                if index = 0 then Some (fullIdText, AbbreviationKind.FullId)
                else 
                    let before = text.[index - 1]
                    let isValid =
                        before = ' ' ||
                        before = '\t' ||
                        before = '\n' ||
                        before = '\r'
                    if isValid then Some (fullIdText, AbbreviationKind.FullId)
                    else None
            | _ -> Some (fullIdText, AbbreviationKind.FullId)

        let getEndId() =
            Debug.Assert(text.Length > 0)
            if text.Length > 1 && x.IsKeywordChar text.[text.Length - 1] && x.IsNonKeywordChar text.[text.Length - 2] then
                let mutable index = text.Length - 2
                while index > 0 && x.IsNonKeywordChar text.[index - 1] do
                    index <- index - 1
                Some ((text.Substring(index)), AbbreviationKind.EndId)
            else None
            
        let getNonId() =
            let testChar c = not (c = ' ' || c = '\t')
            if text.Length > 1 && x.IsNonKeywordChar text.[text.Length - 1] && testChar text.[text.Length - 2] then
                let mutable index = text.Length - 2
                while index > 0 && testChar text.[index - 1] do
                    index <- index - 1
                Some ((text.Substring(index)), AbbreviationKind.NonId)
            else None

        let isAbbreviationTrigger =
            match triggerKeyInput.RawChar with
            | Some c -> not (_wordUtil.IsKeywordChar c)
            | None -> true
        if isAbbreviationTrigger && not (StringUtil.IsNullOrEmpty text) then
            match getFullId() |> Option.orElseWith getEndId |> Option.orElseWith getNonId with
            | None -> None
            | Some (keyText, kind) ->
                let key = KeyNotationUtil.StringToKeyInputSet keyText 
                match x.GetAbbreviation(key, mode, includeGlobal = true) with
                | None -> None
                | Some abbreviation -> 
                    let span = Span(0, keyText.Length)
                    let replacement, remapResult = remap abbreviation
                    let result = { 
                        Abbreviation = abbreviation
                        AbbreviationKind = kind
                        Replacement = replacement
                        ReplacementRemapResult = remapResult
                        OriginalText = text 
                        TriggerKeyInput = triggerKeyInput
                        ReplacedSpan = span
                    }
                    Some result
        else None

    interface IVimLocalAbbreviationMap with
        member x.GlobalAbbreviationMap = _globalAbbreviationMap
        member x.Abbreviations = x.Map.Mappings
        member x.AddAbbreviation(lhs, rhs, allowRemap, mode) = x.Map.Add(lhs, mode, (Abbreviation(lhs, rhs, allowRemap, mode)))
        member x.GetAbbreviation(lhs, mode) = x.Map.Get(lhs, mode)
        member x.GetAbbreviation(lhs, mode, includeGlobal) = x.GetAbbreviation(lhs, mode, includeGlobal)
        member x.GetAbbreviations(mode) = x.Map.Get(mode)
        member x.GetAbbreviations(mode, includeGlobal) = x.GetAbbreviations(mode, includeGlobal)
        member x.RemoveAbbreviation(lhs, mode) = x.Map.Remove(lhs, mode)
        member x.ClearAbbreviations(mode) = x.Map.Clear(mode)
        member x.ClearAbbreviations() = x.Map.Clear()
        member x.Abbreviate(text, triggerKeyInput, mode) = x.Abbreviate text triggerKeyInput mode
        member x.Parse text = x.Parse text
