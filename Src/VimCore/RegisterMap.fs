#light

namespace Vim

/// IRegisterValueBacking implementation for the unnamed register 
type UnnamedRegisterValueBacking (_vimData: IVimData) =
    static let defaultValue
        = RegisterValue(StringUtil.Empty, OperationKind.CharacterWise)
    let map = System.Collections.Generic.Dictionary<int, RegisterValue>();
    member x.RegisterValue = 
        let caretIndex = _vimData.CaretIndex
        if not (map.ContainsKey(caretIndex)) then
            map.[caretIndex] <- defaultValue
        map.[caretIndex]

    member x.SetRegisterValue value =
        let caretIndex = _vimData.CaretIndex
        map.[caretIndex] <- value

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = x.SetRegisterValue value

/// IRegisterValueBacking implementation for the clipboard 
type ClipboardRegisterValueBacking (_device: IClipboardDevice) =

    member x.RegisterValue = 
        let text = _device.Text
        let operationKind = 
            if EditUtil.GetLineBreakLengthAtEnd text > 0 then
                OperationKind.LineWise
            else
                OperationKind.CharacterWise
        RegisterValue(text, operationKind)

    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = x.RegisterValue
            and set value = _device.Text <- value.StringValue

/// IRegisterValueBacking implementation for append registers.  All of the lower
/// case letter registers can be accessed via an upper case version.  The only
/// difference is when accessed via upper case sets of the value should be 
/// append operations
type AppendRegisterValueBacking (_register: Register) =
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = _register.RegisterValue
            and set value = _register.RegisterValue <- _register.RegisterValue.Append value

type LastSearchRegisterValueBacking (_vimData: IVimData) = 
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = RegisterValue(_vimData.LastSearchData.Pattern, OperationKind.CharacterWise)
            and set value = _vimData.LastSearchData <- SearchData(value.StringValue, SearchPath.Forward, false)

type CommandLineBacking (_vimData: IVimData) = 
    interface IRegisterValueBacking  with
        member x.RegisterValue
            with get() = RegisterValue(_vimData.LastCommandLine, OperationKind.CharacterWise)
            and set _ = ()

type LastTextInsertBacking (_vimData: IVimData) = 
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get() = 
                let value = match _vimData.LastTextInsert with Some s -> s | None -> ""
                RegisterValue(value, OperationKind.CharacterWise)
            and set _ = ()

type internal BlackholeRegisterValueBacking() = 
    let mutable _value = RegisterValue(StringUtil.Empty, OperationKind.CharacterWise)
    interface IRegisterValueBacking with
        member x.RegisterValue
            with get() = _value
            and set _ = ()

type internal RegisterMap (_map: Map<RegisterName, Register>) =
    new(vimData: IVimData, clipboard: IClipboardDevice, currentFileNameFunc: unit -> string option) = 
        let unnamedBacking = UnnamedRegisterValueBacking(vimData) :> IRegisterValueBacking
        let clipboardBacking = ClipboardRegisterValueBacking(clipboard) :> IRegisterValueBacking
        let commandLineBacking = CommandLineBacking(vimData) :> IRegisterValueBacking
        let lastTextInsertBacking = LastTextInsertBacking(vimData) :> IRegisterValueBacking
        let fileNameBacking = { new IRegisterValueBacking with
            member x.RegisterValue
                with get() = 
                    let text = 
                        match currentFileNameFunc() with
                        | None -> StringUtil.Empty
                        | Some(str) -> str
                    RegisterValue(text, OperationKind.CharacterWise)
                and set _ = () }

        // Is this an append register 
        let isAppendRegister (name: RegisterName) = name.IsAppend

        let getBacking name = 
            match name with 
            | RegisterName.Unnamed -> unnamedBacking
            | RegisterName.SelectionAndDrop SelectionAndDropRegister.Plus -> clipboardBacking
            | RegisterName.SelectionAndDrop SelectionAndDropRegister.Star  -> clipboardBacking
            | RegisterName.ReadOnly ReadOnlyRegister.Percent -> fileNameBacking
            | RegisterName.ReadOnly ReadOnlyRegister.Colon -> commandLineBacking
            | RegisterName.ReadOnly ReadOnlyRegister.Dot -> lastTextInsertBacking
            | RegisterName.Blackhole -> BlackholeRegisterValueBacking() :> IRegisterValueBacking
            | RegisterName.LastSearchPattern -> LastSearchRegisterValueBacking(vimData) :> IRegisterValueBacking
            | _ -> DefaultRegisterValueBacking() :> IRegisterValueBacking

        // Create the map without the append registers
        let map = 
            RegisterName.All
            |> Seq.filter (fun name -> not (isAppendRegister name))
            |> Seq.map (fun n -> n, Register(n, getBacking n))
            |> Map.ofSeq

        // Now that the map has all of the append registers backing add the actual append
        // registers
        let map = 
            let originalMap = map
            RegisterName.All
            |> Seq.filter isAppendRegister
            |> Seq.fold (fun map (name: RegisterName) ->
                match name.Char with
                | None -> map
                | Some c ->
                    let c = CharUtil.ToLower c 
                    match NamedRegister.OfChar c |> Option.map RegisterName.Named with
                    | None -> map
                    | Some backingRegisterName ->
                        let backingRegister = Map.find backingRegisterName map
                        let value = AppendRegisterValueBacking(backingRegister)
                        let register = Register(name, value)
                        Map.add name register map) originalMap

        RegisterMap(map)

    member x.GetRegister name = Map.find name _map

    member x.SetRegisterValue name value =
        let register = x.GetRegister name
        register.RegisterValue <- value

    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue name value = x.SetRegisterValue name value

