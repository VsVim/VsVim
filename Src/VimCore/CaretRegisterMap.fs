namespace Vim

open System.Collections.Generic

/// Mutable caret index object
type CaretIndex() =
    let mutable _caretIndex = 0
    member x.Value
        with get() = _caretIndex
        and set value = _caretIndex <- value

/// Caret-aware IRegisterValueBacking implementation for the unnamed register.
/// If there are multiple carets, each caret gets its own distinct value
type UnnamedRegisterValueBacking
    (
        _caretIndex: CaretIndex,
        _unnamedRegister: Register
    ) =

    let valueMap = Dictionary<int, RegisterValue>()

    member x.RegisterValue = 
        if _caretIndex.Value = 0 then
            _unnamedRegister.RegisterValue
        else
            match valueMap.TryGetValue(_caretIndex.Value) with
            | true, value -> value
            | _ -> _unnamedRegister.RegisterValue

    member x.SetRegisterValue value =
        if _caretIndex.Value = 0 then
            _unnamedRegister.RegisterValue <- value
        else
            valueMap.[_caretIndex.Value] <- value

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = x.SetRegisterValue value

/// Caret-aware IRegisterValueBacking implementation for the unnamed clipboard
/// register. If there are multiple carets, the primary caret uses the
/// clipboard and each secondary caret uses the caret's unnamed register
type UnnamedClipboardRegisterValueBacking
    (
        _caretIndex: CaretIndex,
        _caretUnnamedRegister: Register,
        _clipboardRegister: Register
    ) =
    member x.RegisterValue = 
        if _caretIndex.Value = 0 then
            _clipboardRegister.RegisterValue
        else
            _caretUnnamedRegister.RegisterValue

    member x.SetRegisterValue (value: RegisterValue) =
        if _caretIndex.Value = 0 then
            _clipboardRegister.RegisterValue <- value
        else
            _caretUnnamedRegister.RegisterValue <- value

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = x.SetRegisterValue value

type CaretRegisterMap
    (
        _registerMap: IRegisterMap,
        _caretIndex: CaretIndex,
        _map: Map<RegisterName, Register>
    ) =

    member x.GetRegister name =
        match Map.tryFind name _map with
        | Some register -> register
        | None -> _registerMap.GetRegister name

    interface ICaretRegisterMap with
        member x.RegisterNames = _registerMap.RegisterNames
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue name value =
            let register = x.GetRegister name
            register.RegisterValue <- value
        member x.CaretIndex
            with get() = _caretIndex.Value
            and set value = _caretIndex.Value <- value

    new(registerMap: IRegisterMap) =

        let caretIndex = CaretIndex()

        let caretUnnamedRegister =
            let unnamedRegister =
                RegisterName.Unnamed
                |> registerMap.GetRegister
            let backing =
                UnnamedRegisterValueBacking(caretIndex, unnamedRegister)
                :> IRegisterValueBacking
            Register(RegisterName.Unnamed, backing)

        let caretUnnamedClipboardRegister =
            let clipboardRegister =
                RegisterName.SelectionAndDrop SelectionAndDropRegister.Star
                |> registerMap.GetRegister
            let backing =
                UnnamedClipboardRegisterValueBacking(caretIndex, caretUnnamedRegister, clipboardRegister)
                :> IRegisterValueBacking
            Register(RegisterName.UnnamedClipboard, backing)

        let map =
            seq {
                yield caretUnnamedRegister
                yield caretUnnamedClipboardRegister
            }
            |> Seq.map (fun register -> register.Name, register)
            |> Map.ofSeq

        CaretRegisterMap(registerMap, caretIndex, map)
