namespace Vim

type CaretIndex() =
    let mutable _caretIndex = 0
    member x.Value
        with get() = _caretIndex
        and set value = _caretIndex <- value

/// Caret-aware IRegisterValueBacking implementation for the unnamed register.
/// If there are multiple carets, each caret gets its own unnamed register
type UnnamedRegisterValueBacking
    (
        _caretIndex: CaretIndex,
        _unnamedRegister: Register
    ) =

    let map = System.Collections.Generic.Dictionary<int, RegisterValue>();

    member x.RegisterValue = 
        let caretIndex = _caretIndex.Value
        if caretIndex = 0 then
            _unnamedRegister.RegisterValue
        else
            if not (map.ContainsKey(caretIndex)) then
                map.[caretIndex] <- _unnamedRegister.RegisterValue
            map.[caretIndex]

    member x.SetRegisterValue value =
        let caretIndex = _caretIndex.Value
        if caretIndex = 0 then
            _unnamedRegister.RegisterValue <- value
        else
            map.[caretIndex] <- value

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = x.SetRegisterValue value

/// Caret-aware IRegisterValueBacking implementation for the unnamed clipboard
/// register. If there are multiple carets, the primary caret uses the
/// clipboard and each secondary caret get its own unnamed register
type UnnamedClipboardRegisterValueBacking
    (
        _caretIndex: CaretIndex,
        _caretUnnamedRegister: Register,
        _clipboardRegister: Register
    ) =
    member x.RegisterValue = 
        let caretIndex = _caretIndex.Value
        if caretIndex = 0 then
            _clipboardRegister.RegisterValue
        else
            _caretUnnamedRegister.RegisterValue

    member x.SetRegisterValue (value: RegisterValue) =
        let caretIndex = _caretIndex.Value
        if caretIndex = 0 then
            _clipboardRegister.RegisterValue <- value
        else
            _caretUnnamedRegister.RegisterValue <- value

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = x.SetRegisterValue value

type CaretRegisterMap
    (
        _caretIndex: CaretIndex,
        _map: Map<RegisterName, Register>
    ) =

    interface ICaretRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = Map.find name _map
        member x.SetRegisterValue name value =
            let register = Map.find name _map
            register.RegisterValue <- value
        member x.CaretIndex
            with get() = _caretIndex.Value
            and set value = _caretIndex.Value <- value

    new(registerMap: IRegisterMap) =

        let caretIndex = CaretIndex()

        let unnamedRegister =
            RegisterName.Unnamed
            |> registerMap.GetRegister
        let clipboardRegister =
            RegisterName.SelectionAndDrop SelectionAndDropRegister.Star
            |> registerMap.GetRegister

        let caretUnnamedRegister =
            let backing =
                UnnamedRegisterValueBacking(caretIndex, unnamedRegister)
                :> IRegisterValueBacking
            Register(RegisterName.Unnamed, backing)
        let caretUnnamedClipboardRegister =
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
        CaretRegisterMap(caretIndex, map)
