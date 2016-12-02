﻿#light

namespace Vim

/// IRegisterValueBacking implementation for the clipboard 
type ClipboardRegisterValueBacking (_device : IClipboardDevice) =

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
type AppendRegisterValueBacking (_register : Register) =
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = _register.RegisterValue
            and set value = _register.RegisterValue <- _register.RegisterValue.Append value

type LastSearchRegisterValueBacking (_vimData : IVimData) = 
    interface IRegisterValueBacking with
        member x.RegisterValue 
            with get () = RegisterValue(_vimData.LastSearchData.Pattern, OperationKind.CharacterWise)
            and set value = _vimData.LastSearchData <- SearchData(value.StringValue, SearchPath.Forward, false)

type CommandLineBacking (_vimData : IVimData) = 
    interface IRegisterValueBacking  with
        member x.RegisterValue
            with get() = RegisterValue(_vimData.LastCommandLine, OperationKind.CharacterWise)
            and set value = ()

type internal RegisterMap (_map : Map<RegisterName, Register>) =
    new(vimData : IVimData, clipboard : IClipboardDevice, currentFileNameFunc : unit -> string option) = 
        let clipboardBacking = ClipboardRegisterValueBacking(clipboard) :> IRegisterValueBacking
        let commandLineBacking = CommandLineBacking(vimData) :> IRegisterValueBacking
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
        let isAppendRegister (name : RegisterName) = name.IsAppend

        let getBacking name = 
            match name with 
            | RegisterName.SelectionAndDrop SelectionAndDropRegister.Plus -> clipboardBacking
            | RegisterName.SelectionAndDrop SelectionAndDropRegister.Star  -> clipboardBacking
            | RegisterName.ReadOnly ReadOnlyRegister.Percent -> fileNameBacking
            | RegisterName.ReadOnly ReadOnlyRegister.Colon -> commandLineBacking
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
            |> Seq.fold (fun map (name : RegisterName) ->
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
        // RTODO: won't need this when we have a proper BlackHole backing
        if name <> RegisterName.Blackhole then
            let register = x.GetRegister name
            register.RegisterValue <- value

    interface IRegisterMap with
        member x.RegisterNames = _map |> Seq.map (fun pair -> pair.Key)
        member x.GetRegister name = x.GetRegister name
        member x.SetRegisterValue name value = x.SetRegisterValue name value

