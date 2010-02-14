#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open System.Windows.Input

type internal SettingsMap
    (
        _rawData : (string*string*SettingKind*SettingValue) seq,
        _isGlobal : bool ) =


    /// Create the settings off of the default map
    let mutable _settings =
         _rawData
         |> Seq.map (fun (name,abbrev,kind,value) -> { Name=name; Abbreviation=abbrev;Kind=kind; DefaultValue=value;Value=value; IsGlobal=_isGlobal})
         |> Seq.map (fun setting -> (setting.Name,setting))
         |> Map.ofSeq

    member x.AllSettings = _settings |> Map.toSeq |> Seq.map (fun (_,value) -> value)
    member x.OwnsSetting settingName = x.GetSetting settingName |> Option.isSome

    member x.TrySetValue settingName value =

        /// Determine if the value and the kind are compatible
        let doesValueMatchKind kind = 
            match kind,value with
            | (NumberKind,NumberValue(_)) -> true
            | (StringKind,StringValue(_)) -> true
            | (BooleanKind,BooleanValue(_)) -> true
            | (_, NoValue) -> true
            | _ -> false

        match _settings |> Map.tryFind settingName with
        | None -> false
        | Some(setting) ->
            if doesValueMatchKind setting.Kind then
                let setting = { setting with Value=value }
                _settings <- _settings |> Map.add settingName setting
                true
            else false

    member x.GetSetting settingName = 
        match _settings |> Map.tryFind settingName with
        | Some(s) -> Some(s)
        | None -> 
            _settings 
            |> Map.toSeq 
            |> Seq.map (fun (_,value) -> value) 
            |> Seq.tryFind (fun setting -> setting.Abbreviation = settingName)
            

    /// Get a boolean setting value.  Will throw if the setting name does not exist
    member x.GetBoolValue settingName = 
        let setting = _settings |> Map.find settingName
        match setting.Value,setting.DefaultValue with 
        | BooleanValue(b),_ -> b
        | NoValue,BooleanValue(b) -> b
        | _ -> failwith "Invalid"

    member x.GetStringValue settingName =
        let setting = _settings |> Map.find settingName
        match setting.Value,setting.DefaultValue with
        | StringValue(s),_ -> s
        | NoValue,StringValue(s) -> s
        | _ -> failwith "Invalid"

    member x.GetNumberValue settingName =
        let setting = _settings |> Map.find settingName
        match setting.Value,setting.DefaultValue with
        | NumberValue(n),_ -> n
        | NoValue,NumberValue(n) -> n
        | _ -> failwith "Invalid"

type internal GlobalSettings() =

    static let DisableCommandLet = KeyInput(System.Char.MinValue, Key.F12, ModifierKeys.Control ||| ModifierKeys.Shift);
    static let IgnoreCaseName = "ignorecase"
    static let ShiftWidthName = "shiftwidth"

    static let GlobalSettings = 
        [|
            ( IgnoreCaseName,"ic", BooleanKind, BooleanValue(false) );
            ( ShiftWidthName, "sw", NumberKind, NumberValue(4) )
        |]

    let _map = SettingsMap(GlobalSettings, true)

    static member DisableCommand = DisableCommandLet

    interface IVimGlobalSettings with
        // IVimSettings

        member x.AllSettings = _map.AllSettings
        member x.TrySetValue settingName value = _map.TrySetValue settingName value
        member x.GetSetting settingName = _map.GetSetting settingName

        // IVimGlobalSettings 
        member x.IgnoreCase
            with get()  = _map.GetBoolValue IgnoreCaseName
            and set value = _map.TrySetValue IgnoreCaseName (BooleanValue(value)) |> ignore
        member x.ShiftWidth  
            with get() = _map.GetNumberValue ShiftWidthName
            and set value = _map.TrySetValue ShiftWidthName (NumberValue(value)) |> ignore

        member x.DisableCommand = DisableCommandLet

type internal LocalSettings
    ( 
        _global : IVimGlobalSettings,
        _textView : ITextView ) =
    
    static let ScrollName = "scroll"

    static let LocalSettings =
        [|
            ( ScrollName, "scr", NumberKind, NumberValue(25) )
        |]

    let _map = SettingsMap(LocalSettings, false)

    interface IVimLocalSettings with 
        // IVimSettings
        
        member x.AllSettings = _map.AllSettings |> Seq.append _global.AllSettings
        member x.TrySetValue settingName value = 
            if _map.OwnsSetting settingName then _map.TrySetValue settingName value
            else _global.TrySetValue settingName value
        member x.GetSetting settingName =
            if _map.OwnsSetting settingName then _map.GetSetting settingName
            else _global.GetSetting settingName

        member x.GlobalSettings = _global
        member x.Scroll 
            with get() = _map.GetNumberValue ScrollName
            and set value = _map.TrySetValue ScrollName (NumberValue(value)) |> ignore
    

