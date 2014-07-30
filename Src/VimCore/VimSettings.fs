#light

namespace Vim
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods
open System.ComponentModel.Composition
open System.Collections.Generic
open System.Text
open Vim.GlobalSettingNames
open Vim.LocalSettingNames
open Vim.WindowSettingNames
open StringBuilderExtensions
open CollectionExtensions

// TODO: We need to add verification for setting options which can contain
// a finite list of values.  For example backspace, virtualedit, etc ...  Setting
// them to an invalid value should produce an error

type SettingValueParseFunc = string -> SettingValue option

type internal SettingsMap
    (
        _rawData : Setting seq
    ) as this =

    let _settingChangedEvent = StandardEvent<SettingEventArgs>()

    /// Map from full setting name to the actual Setting
    let mutable _settingMap = Dictionary<string, Setting>()

    /// Map from the abbreviated setting name to the full setting name
    let mutable _shortToFullNameMap = Dictionary<string, string>()

    /// Custom parsing function for a given setting name
    let mutable _customParseMap = Dictionary<string, SettingValueParseFunc>()

    do
        _rawData
        |> Seq.iter (fun setting -> this.AddSetting setting)

    member x.AllSettings = _settingMap.Values |> List.ofSeq

    member x.OwnsSetting settingName = x.GetSetting settingName |> Option.isSome

    member x.SettingChanged = _settingChangedEvent.Publish

    member x.AddSetting (setting : Setting) =
        _settingMap.Add(setting.Name, setting)
        _shortToFullNameMap.Add(setting.Abbreviation, setting.Name)

    /// Replace a Setting with a new value
    member x.ReplaceSetting setting = 
        Contract.Assert (_settingMap.ContainsKey setting.Name)
        _settingMap.[setting.Name] <- setting

        let args = SettingEventArgs(setting, true)
        _settingChangedEvent.Trigger x args

    member x.AddSettingValueParseFunc settingNameOrAbbrev settingValueParseFunc =
        let name = x.GetFullName settingNameOrAbbrev
        _customParseMap.Add(name, settingValueParseFunc)

    member x.GetFullName settingNameOrAbbrev = 
        match _shortToFullNameMap.TryGetValueEx settingNameOrAbbrev with
        | Some fullName -> fullName
        | None -> settingNameOrAbbrev

    member x.TrySetValue settingNameOrAbbrev (value : SettingValue) =
        let name = x.GetFullName settingNameOrAbbrev
        match _settingMap.TryGetValueEx name with
        | None -> false
        | Some setting -> 
            let isValueChanged = value <> setting.Value
            match setting.LiveSettingValue.UpdateValue value with
            | Some value ->
                let setting = { setting with LiveSettingValue = value }
                _settingMap.[name] <- setting
                _settingChangedEvent.Trigger x (SettingEventArgs(setting, isValueChanged))
                true
            | None -> false

    member x.TrySetValueFromString settingNameOrAbbrev strValue = 
        match x.GetSetting settingNameOrAbbrev with
        | None -> false
        | Some setting ->
            match x.ConvertStringToValue setting strValue with
            | None -> false
            | Some value -> x.TrySetValue setting.Name value

    member x.GetSetting settingNameOrAbbrev : Setting option =
        let name = x.GetFullName settingNameOrAbbrev
        _settingMap.TryGetValueEx name

    /// Get a boolean setting value.  Will throw if the setting name does not exist
    member x.GetBoolValue settingNameOrAbbrev = 
        let setting = x.GetSetting settingNameOrAbbrev |> Option.get
        match setting.Value with
        | SettingValue.Toggle b -> b 
        | SettingValue.Number _ -> failwith "invalid"
        | SettingValue.String _ -> failwith "invalid"

    /// Get a string setting value.  Will throw if the setting name does not exist
    member x.GetStringValue settingNameOrAbbrev =
        let setting = x.GetSetting settingNameOrAbbrev |> Option.get
        match setting.Value with
        | SettingValue.String s -> s
        | SettingValue.Number _ -> failwith "invalid"
        | SettingValue.Toggle _ -> failwith "invalid"

    /// Get a number setting value.  Will throw if the setting name does not exist
    member x.GetNumberValue settingNameOrAbbrev =
        let setting = x.GetSetting settingNameOrAbbrev |> Option.get
        match setting.Value with
        | SettingValue.Number n -> n
        | SettingValue.String _ -> failwith "invalid"
        | SettingValue.Toggle _ -> failwith "invalid"

    member x.ConvertStringToValue (setting : Setting) (str : string) = 
        match _customParseMap.TryGetValueEx setting.Name with
        | None -> x.ConvertStringToValueCore str setting.Kind
        | Some func ->
            match func str with
            | Some settingValue -> Some settingValue
            | None -> x.ConvertStringToValueCore str setting.Kind

    member x.ConvertStringToValueCore str kind =
        let convertToNumber() = 
            let ret,value = System.Int32.TryParse str
            if ret then Some (SettingValue.Number value) else None
        let convertToBoolean() =
            let ret,value = System.Boolean.TryParse str
            if ret then Some (SettingValue.Toggle value) else None
        match kind with
        | SettingKind.Number -> convertToNumber()
        | SettingKind.Toggle -> convertToBoolean()
        | SettingKind.String -> Some (SettingValue.String str)

type internal GlobalSettings() =

    /// Custom parsing for the old 'vi' style values of 'backspace'.  For normal values default
    /// to the standard parsing behavior
    static let ParseBackspaceValue str = 
        match str with
        | "0" -> SettingValue.String "" |> Some
        | "1" -> SettingValue.String "indent,eol" |> Some
        | "2" -> SettingValue.String "indent,eol,start" |> Some
        | _ -> None

    static let GlobalSettingInfoList = 
        [|
            (BackspaceName, "bs", SettingValue.String "")
            (CaretOpacityName, CaretOpacityName, SettingValue.Number 65)
            (ClipboardName, "cb", SettingValue.String "")
            (CurrentDirectoryPathName, "cd", SettingValue.String ",,")
            (GlobalDefaultName, "gd", SettingValue.Toggle false)
            (HighlightSearchName, "hls", SettingValue.Toggle false)
            (HistoryName, "hi", SettingValue.Number(VimConstants.DefaultHistoryLength))
            (IncrementalSearchName, "is", SettingValue.Toggle false)
            (IgnoreCaseName,"ic", SettingValue.Toggle false)
            (JoinSpacesName, "js", SettingValue.Toggle true)
            (KeyModelName, "km", SettingValue.String "")
            (LastStatusName, "ls", SettingValue.Number 0)
            (MagicName, MagicName, SettingValue.Toggle true)
            (MaxMapDepth, "mmd", SettingValue.Number 1000)
            (MouseModelName, "mousem", SettingValue.String "popup")
            (PathName,"pa", SettingValue.String ".,,")
            (ParagraphsName, "para", SettingValue.String "IPLPPPQPP TPHPLIPpLpItpplpipbp")
            (SectionsName, "sect", SettingValue.String "SHNHH HUnhsh")
            (SelectionName, "sel", SettingValue.String "inclusive")
            (SelectModeName, "slm", SettingValue.String "")
            (ScrollOffsetName, "so", SettingValue.Number 0)
            (ShellName, "sh", "ComSpec" |> SystemUtil.GetEnvironmentVariable |> SettingValue.String)
            (ShellFlagName, "shcf", SettingValue.String "/c")
            (SmartCaseName, "scs", SettingValue.Toggle false)
            (StartOfLineName, "sol", SettingValue.Toggle true)
            (StatusLineName, "stl", SettingValue.String "")
            (TabStopName, "ts", SettingValue.Number 8)
            (TildeOpName, "top", SettingValue.Toggle false)
            (TimeoutName, "to", SettingValue.Toggle true)
            (TimeoutExName, TimeoutExName, SettingValue.Toggle false)
            (TimeoutLengthName, "tm", SettingValue.Number 1000)
            (TimeoutLengthExName, "ttm", SettingValue.Number -1)
            (VimRcName, VimRcName, SettingValue.String(StringUtil.empty))
            (VimRcPathsName, VimRcPathsName, SettingValue.String(StringUtil.empty))
            (VirtualEditName, "ve", SettingValue.String(StringUtil.empty))
            (VisualBellName, "vb", SettingValue.Toggle false)
            (WhichWrapName, "ww", SettingValue.String "b,s")
            (WrapScanName, "ws", SettingValue.Toggle true)
        |]

    static let GlobalSettingList = 
        GlobalSettingInfoList
        |> Seq.map (fun (name, abbrev, defaultValue) -> { Name = name; Abbreviation = abbrev; LiveSettingValue = LiveSettingValue.Create defaultValue; IsGlobal = true })

    let _map = 
        let settingsMap = SettingsMap(GlobalSettingList)
        settingsMap.AddSettingValueParseFunc BackspaceName ParseBackspaceValue
        settingsMap

    /// Mappings between the setting names and the actual options
    static let ClipboardOptionsMapping = 
        [
            ("unnamed", ClipboardOptions.Unnamed)
            ("autoselect", ClipboardOptions.AutoSelect)
            ("autoselectml", ClipboardOptions.AutoSelectMl)
        ]

    /// Mappings between the setting names and the actual options
    static let SelectModeOptionsMapping = 
        [  
            ("mouse", SelectModeOptions.Mouse)
            ("key", SelectModeOptions.Keyboard)
            ("cmd", SelectModeOptions.Command)
        ]

    /// Mappings between the setting names and the actual options
    static let KeyModelOptionsMapping =
        [
            ("startsel", KeyModelOptions.StartSelection)
            ("stopsel", KeyModelOptions.StopSelection)
        ]

    static member DisableAllCommand = KeyInputUtil.ApplyModifiersToVimKey VimKey.F12 (KeyModifiers.Control ||| KeyModifiers.Shift)

    member x.AddCustomSetting name abbrevation customSettingSource = 
        let liveSettingValue = LiveSettingValue.Custom (name, customSettingSource)
        let setting = { Name = name; Abbreviation = abbrevation; LiveSettingValue = liveSettingValue; IsGlobal = true }
        _map.AddSetting setting

    member x.IsCommaSubOptionPresent optionName suboptionName =
        _map.GetStringValue optionName
        |> StringUtil.split ','
        |> Seq.exists (fun x -> StringUtil.isEqual suboptionName x)

    /// Convert a comma separated option into a set of type safe options
    member x.GetCommaOptions name mappingList emptyOption combineFunc = 
        _map.GetStringValue name 
        |> StringUtil.split ',' 
        |> Seq.fold (fun (options : 'a) (current : string)->
            match List.tryFind (fun (name, _) -> name = current) mappingList with
            | None -> options
            | Some (_, value) -> combineFunc options value) emptyOption

    /// Convert a type safe set of options into a comma separated string
    member x.SetCommaOptions name mappingList options testFunc = 
        let settingValue = 
            mappingList
            |> Seq.ofList
            |> Seq.map (fun (name, value) ->
                if testFunc options value then 
                    Some name
                else 
                    None)
            |> SeqUtil.filterToSome
            |> String.concat ","
        _map.TrySetValue name (SettingValue.String settingValue) |> ignore

    /// Parse out the 'cdpath' or 'path' option into a strongly typed collection.  The format
    /// for the collection is described in the documentation for 'path' but applies equally
    /// to both options
    member x.GetPathOptionList text = 
        let length = String.length text
        let list = List<PathOption>()

        let addOne text =
            match text with
            | "." -> list.Add PathOption.CurrentFile
            | "" -> list.Add PathOption.CurrentDirectory
            | _ -> list.Add (PathOption.Named text)

        let builder = StringBuilder()
        let mutable i = 0
        while i < length do
            match text.[i] with
            | '\\' ->
                if i + 1 < length then
                    builder.AppendChar text.[i + 1]
                i <- i + 2
            | ',' -> 
                // Ignore the case where the string begins with ','.  This is used to 
                // have the first entry be ',,' and have the current directory be included
                if i > 0 then 
                    addOne (builder.ToString())
                    builder.Length <- 0 

                i <- i + 1
            | ' ' -> 
                // Space is also a separator
                addOne (builder.ToString())
                builder.Length <- 0
                i <- i + 1
            | c -> 
                builder.AppendChar c
                i <- i + 1

        if builder.Length > 0 then
            addOne (builder.ToString())
                    
        List.ofSeq list

    member x.SelectionKind = 
        match _map.GetStringValue SelectionName with
        | "inclusive" -> SelectionKind.Inclusive
        | "old" -> SelectionKind.Exclusive
        | _ -> SelectionKind.Exclusive

    interface IVimGlobalSettings with
        // IVimSettings

        member x.AllSettings = _map.AllSettings
        member x.TrySetValue settingName value = _map.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = _map.TrySetValueFromString settingName strValue
        member x.GetSetting settingName = _map.GetSetting settingName

        // IVimGlobalSettings 
        member x.AddCustomSetting name abbrevation customSettingSource = x.AddCustomSetting name abbrevation customSettingSource
        member x.Backspace 
            with get() = _map.GetStringValue BackspaceName
            and set value = _map.TrySetValueFromString BackspaceName value |> ignore
        member x.CaretOpacity
            with get() = _map.GetNumberValue CaretOpacityName
            and set value = _map.TrySetValue CaretOpacityName (SettingValue.Number value) |> ignore
        member x.Clipboard
            with get() = _map.GetStringValue ClipboardName
            and set value = _map.TrySetValue ClipboardName (SettingValue.String value) |> ignore
        member x.ClipboardOptions
            with get() = x.GetCommaOptions ClipboardName ClipboardOptionsMapping ClipboardOptions.None (fun x y -> x ||| y)
            and set value = x.SetCommaOptions ClipboardName ClipboardOptionsMapping value Util.IsFlagSet
        member x.CurrentDirectoryPath
            with get() = _map.GetStringValue CurrentDirectoryPathName
            and set value = _map.TrySetValue CurrentDirectoryPathName (SettingValue.String value) |> ignore
        member x.CurrentDirectoryPathList = x.GetPathOptionList (_map.GetStringValue CurrentDirectoryPathName)
        member x.GlobalDefault
            with get() = _map.GetBoolValue GlobalDefaultName
            and set value = _map.TrySetValue GlobalDefaultName (SettingValue.Toggle value) |> ignore
        member x.HighlightSearch
            with get() = _map.GetBoolValue HighlightSearchName
            and set value = _map.TrySetValue HighlightSearchName (SettingValue.Toggle value) |> ignore
        member x.History
            with get () = _map.GetNumberValue HistoryName
            and set value = _map.TrySetValue HistoryName (SettingValue.Number value) |> ignore
        member x.IgnoreCase
            with get()  = _map.GetBoolValue IgnoreCaseName
            and set value = _map.TrySetValue IgnoreCaseName (SettingValue.Toggle value) |> ignore
        member x.IncrementalSearch
            with get() = _map.GetBoolValue IncrementalSearchName
            and set value = _map.TrySetValue IncrementalSearchName (SettingValue.Toggle value) |> ignore
        member x.IsSelectionInclusive = x.SelectionKind = SelectionKind.Inclusive
        member x.IsSelectionPastLine = 
            match _map.GetStringValue SelectionName with
            | "exclusive" -> true
            | "inclusive" -> true
            | _ -> false
        member x.JoinSpaces 
            with get() = _map.GetBoolValue JoinSpacesName
            and set value = _map.TrySetValue JoinSpacesName (SettingValue.Toggle value) |> ignore
        member x.KeyModel 
            with get() = _map.GetStringValue KeyModelName
            and set value = _map.TrySetValue KeyModelName (SettingValue.String value) |> ignore
        member x.KeyModelOptions
            with get() = x.GetCommaOptions KeyModelName KeyModelOptionsMapping KeyModelOptions.None (fun x y -> x ||| y)
            and set value = x.SetCommaOptions KeyModelName KeyModelOptionsMapping value Util.IsFlagSet
        member x.LastStatus
            with get() = _map.GetNumberValue LastStatusName
            and set value = _map.TrySetValue LastStatusName (SettingValue.Number value) |> ignore
        member x.Magic
            with get() = _map.GetBoolValue MagicName
            and set value = _map.TrySetValue MagicName (SettingValue.Toggle value) |> ignore
        member x.MaxMapDepth
            with get() = _map.GetNumberValue MaxMapDepth
            and set value = _map.TrySetValue MaxMapDepth (SettingValue.Number value) |> ignore
        member x.MouseModel 
            with get() = _map.GetStringValue MouseModelName
            and set value = _map.TrySetValue MouseModelName (SettingValue.String value) |> ignore
        member x.Paragraphs
            with get() = _map.GetStringValue ParagraphsName
            and set value = _map.TrySetValue ParagraphsName (SettingValue.String value) |> ignore
        member x.Path
            with get() = _map.GetStringValue PathName
            and set value = _map.TrySetValue PathName (SettingValue.String value) |> ignore
        member x.PathList = x.GetPathOptionList (_map.GetStringValue PathName)
        member x.ScrollOffset
            with get() = _map.GetNumberValue ScrollOffsetName
            and set value = _map.TrySetValue ScrollOffsetName (SettingValue.Number value) |> ignore
        member x.Sections
            with get() = _map.GetStringValue SectionsName
            and set value = _map.TrySetValue SectionsName (SettingValue.String value) |> ignore
        member x.Selection
            with get() = _map.GetStringValue SelectionName
            and set value = _map.TrySetValue SelectionName (SettingValue.String value) |> ignore
        member x.SelectionKind = x.SelectionKind
        member x.SelectMode 
            with get() = _map.GetStringValue SelectModeName
            and set value = _map.TrySetValue SelectModeName (SettingValue.String value) |> ignore
        member x.SelectModeOptions 
            with get() = x.GetCommaOptions SelectModeName SelectModeOptionsMapping SelectModeOptions.None (fun x y -> x ||| y) 
            and set value = x.SetCommaOptions SelectModeName SelectModeOptionsMapping value Util.IsFlagSet
        member x.Shell 
            with get() = _map.GetStringValue ShellName
            and set value = _map.TrySetValue ShellName (SettingValue.String value) |> ignore
        member x.ShellFlag
            with get() = _map.GetStringValue ShellFlagName
            and set value = _map.TrySetValue ShellFlagName (SettingValue.String value) |> ignore
        member x.SmartCase
            with get() = _map.GetBoolValue SmartCaseName
            and set value = _map.TrySetValue SmartCaseName (SettingValue.Toggle value) |> ignore
        member x.StartOfLine 
            with get() = _map.GetBoolValue StartOfLineName
            and set value = _map.TrySetValue StartOfLineName (SettingValue.Toggle value) |> ignore
        member x.StatusLine
            with get() = _map.GetStringValue StatusLineName
            and set value = _map.TrySetValue StatusLineName (SettingValue.String value) |> ignore
        member x.TildeOp
            with get() = _map.GetBoolValue TildeOpName
            and set value = _map.TrySetValue TildeOpName (SettingValue.Toggle value) |> ignore
        member x.Timeout
            with get() = _map.GetBoolValue TimeoutName
            and set value = _map.TrySetValue TimeoutName (SettingValue.Toggle value) |> ignore
        member x.TimeoutEx
            with get() = _map.GetBoolValue TimeoutExName
            and set value = _map.TrySetValue TimeoutExName (SettingValue.Toggle value) |> ignore
        member x.TimeoutLength
            with get() = _map.GetNumberValue TimeoutLengthName
            and set value = _map.TrySetValue TimeoutLengthName (SettingValue.Number value) |> ignore
        member x.TimeoutLengthEx
            with get() = _map.GetNumberValue TimeoutLengthExName
            and set value = _map.TrySetValue TimeoutLengthExName (SettingValue.Number value) |> ignore
        member x.VimRc 
            with get() = _map.GetStringValue VimRcName
            and set value = _map.TrySetValue VimRcName (SettingValue.String value) |> ignore
        member x.VimRcPaths 
            with get() = _map.GetStringValue VimRcPathsName
            and set value = _map.TrySetValue VimRcPathsName (SettingValue.String value) |> ignore
        member x.VirtualEdit
            with get() = _map.GetStringValue VirtualEditName
            and set value = _map.TrySetValue VirtualEditName (SettingValue.String value) |> ignore
        member x.VisualBell
            with get() = _map.GetBoolValue VisualBellName
            and set value = _map.TrySetValue VisualBellName (SettingValue.Toggle value) |> ignore
        member x.WhichWrap
            with get() = _map.GetStringValue WhichWrapName
            and set value = _map.TrySetValue WhichWrapName (SettingValue.String value) |> ignore
        member x.WrapScan
            with get() = _map.GetBoolValue WrapScanName
            and set value = _map.TrySetValue WrapScanName (SettingValue.Toggle value) |> ignore
        member x.DisableAllCommand = GlobalSettings.DisableAllCommand
        member x.IsBackspaceEol = x.IsCommaSubOptionPresent BackspaceName "eol"
        member x.IsBackspaceIndent = x.IsCommaSubOptionPresent BackspaceName "indent"
        member x.IsBackspaceStart = x.IsCommaSubOptionPresent BackspaceName "start"
        member x.IsVirtualEditOneMore = x.IsCommaSubOptionPresent VirtualEditName "onemore"
        member x.IsWhichWrapSpaceLeft = x.IsCommaSubOptionPresent WhichWrapName "b"
        member x.IsWhichWrapSpaceRight = x.IsCommaSubOptionPresent WhichWrapName "s"
        member x.IsWhichWrapCharLeft = x.IsCommaSubOptionPresent WhichWrapName "h"
        member x.IsWhichWrapCharRight = x.IsCommaSubOptionPresent WhichWrapName "l"
        member x.IsWhichWrapArrowLeft = x.IsCommaSubOptionPresent WhichWrapName "<"
        member x.IsWhichWrapArrowRight = x.IsCommaSubOptionPresent WhichWrapName ">"
        member x.IsWhichWrapTilde = x.IsCommaSubOptionPresent WhichWrapName "~"
        member x.IsWhichWrapArrowLeftInsert = x.IsCommaSubOptionPresent WhichWrapName "["
        member x.IsWhichWrapArrowRightInsert = x.IsCommaSubOptionPresent WhichWrapName "]"

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

type internal LocalSettings
    ( 
        _globalSettings : IVimGlobalSettings
    ) =

    static let LocalSettingInfoList =
        [|
            (AutoIndentName, "ai", SettingValue.Toggle false)
            (ExpandTabName, "et", SettingValue.Toggle false)
            (NumberName, "nu", SettingValue.Toggle false)
            (NumberFormatsName, "nf", SettingValue.String "octal,hex")
            (SoftTabStopName, "sts", SettingValue.Number 0)
            (ShiftWidthName, "sw", SettingValue.Number 8)
            (TabStopName, "ts", SettingValue.Number 8)
            (QuoteEscapeName, "qe", SettingValue.String @"\")
        |]

    static let LocalSettingList = 
        LocalSettingInfoList
        |> Seq.map (fun (name, abbrev, defaultValue) -> { Name = name; Abbreviation = abbrev; LiveSettingValue = LiveSettingValue.Create defaultValue; IsGlobal = false })

    let _map = SettingsMap(LocalSettingList)

    member x.Map = _map

    static member Copy (settings : IVimLocalSettings) = 
        let copy = LocalSettings(settings.GlobalSettings)
        settings.AllSettings
        |> Seq.filter (fun s -> not s.IsGlobal && not s.IsValueCalculated)
        |> Seq.iter (fun s -> copy.Map.TrySetValue s.Name s.Value |> ignore)
        copy :> IVimLocalSettings

    member x.IsNumberFormatSupported numberFormat =

        // The format is supported if the name is in the comma delimited value
        let isSupported format = 
            _map.GetStringValue NumberFormatsName
            |> StringUtil.split ','
            |> Seq.exists (fun value -> value = format)

        match numberFormat with
        | NumberFormat.Decimal ->
            // This is always supported independent of the option value
            true
        | NumberFormat.Octal ->
            isSupported "octal"
        | NumberFormat.Hex ->
            isSupported "hex"
        | NumberFormat.Alpha ->
            isSupported "alpha"

    interface IVimLocalSettings with 
        // IVimSettings
        
        member x.AllSettings = _map.AllSettings |> Seq.append _globalSettings.AllSettings |> List.ofSeq
        member x.TrySetValue settingName value = 
            if _map.OwnsSetting settingName then _map.TrySetValue settingName value
            else _globalSettings.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = 
            if _map.OwnsSetting settingName then _map.TrySetValueFromString settingName strValue
            else _globalSettings.TrySetValueFromString settingName strValue
        member x.GetSetting settingName =
            if _map.OwnsSetting settingName then _map.GetSetting settingName
            else _globalSettings.GetSetting settingName

        member x.GlobalSettings = _globalSettings
        member x.AutoIndent
            with get() = _map.GetBoolValue AutoIndentName
            and set value = _map.TrySetValue AutoIndentName (SettingValue.Toggle value) |> ignore
        member x.ExpandTab
            with get() = _map.GetBoolValue ExpandTabName
            and set value = _map.TrySetValue ExpandTabName (SettingValue.Toggle value) |> ignore
        member x.Number
            with get() = _map.GetBoolValue NumberName
            and set value = _map.TrySetValue NumberName (SettingValue.Toggle value) |> ignore
        member x.NumberFormats
            with get() = _map.GetStringValue NumberFormatsName
            and set value = _map.TrySetValue NumberFormatsName (SettingValue.String value) |> ignore
        member x.SoftTabStop  
            with get() = _map.GetNumberValue SoftTabStopName
            and set value = _map.TrySetValue SoftTabStopName (SettingValue.Number value) |> ignore
        member x.ShiftWidth  
            with get() = _map.GetNumberValue ShiftWidthName
            and set value = _map.TrySetValue ShiftWidthName (SettingValue.Number value) |> ignore
        member x.TabStop
            with get() = _map.GetNumberValue TabStopName
            and set value = _map.TrySetValue TabStopName (SettingValue.Number value) |> ignore
        member x.QuoteEscape
            with get() = _map.GetStringValue QuoteEscapeName
            and set value = _map.TrySetValue QuoteEscapeName (SettingValue.String value) |> ignore

        member x.IsNumberFormatSupported numberFormat = x.IsNumberFormatSupported numberFormat

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

type internal WindowSettings
    ( 
        _globalSettings : IVimGlobalSettings,
        _textView : ITextView option
    ) as this =

    static let WindowSettingInfoList =
        [|
            (CursorLineName, "cul", SettingValue.Toggle false)
            (ScrollName, "scr", SettingValue.Number 25)
            (WrapName, WrapName, SettingValue.Toggle false)
        |]

    static let WindowSettingList =
        WindowSettingInfoList
        |> Seq.map (fun (name, abbrev, defaultValue) -> { Name = name; Abbreviation = abbrev; LiveSettingValue = LiveSettingValue.Create defaultValue; IsGlobal = false })

    let _map = SettingsMap(WindowSettingList)

    do
        let setting = _map.GetSetting ScrollName |> Option.get
        let liveSettingValue = LiveSettingValue.CalculatedNumber (None, this.CalculateScroll)
        let setting = { setting with LiveSettingValue = liveSettingValue } 
        _map.ReplaceSetting setting

    new (settings) = WindowSettings(settings, None)
    new (settings, textView : ITextView) = WindowSettings(settings, Some textView)

    member x.Map = _map

    /// Calculate the scroll value as specified in the Vim documentation.  Should be half the number of 
    /// visible lines 
    member x.CalculateScroll() =
        let defaultValue = 10
        match _textView with
        | None -> defaultValue
        | Some textView -> int (textView.ViewportHeight / textView.LineHeight / 2.0 + 0.5)

    static member Copy (settings : IVimWindowSettings) = 
        let copy = WindowSettings(settings.GlobalSettings)
        settings.AllSettings
        |> Seq.filter (fun s -> not s.IsGlobal && not s.IsValueCalculated)
        |> Seq.iter (fun s -> copy.Map.TrySetValue s.Name s.Value |> ignore)
        copy :> IVimWindowSettings

    interface IVimWindowSettings with 
        member x.AllSettings = _map.AllSettings |> Seq.append _globalSettings.AllSettings |> List.ofSeq
        member x.TrySetValue settingName value = 
            if _map.OwnsSetting settingName then _map.TrySetValue settingName value
            else _globalSettings.TrySetValue settingName value
        member x.TrySetValueFromString settingName strValue = 
            if _map.OwnsSetting settingName then _map.TrySetValueFromString settingName strValue
            else _globalSettings.TrySetValueFromString settingName strValue
        member x.GetSetting settingName =
            if _map.OwnsSetting settingName then _map.GetSetting settingName
            else _globalSettings.GetSetting settingName
        member x.GlobalSettings = _globalSettings

        member x.CursorLine 
            with get() = _map.GetBoolValue CursorLineName
            and set value = _map.TrySetValue CursorLineName (SettingValue.Toggle value) |> ignore
        member x.Scroll 
            with get() = _map.GetNumberValue ScrollName
            and set value = _map.TrySetValue ScrollName (SettingValue.Number value) |> ignore
        member x.Wrap
            with get() = _map.GetBoolValue WrapName
            and set value = _map.TrySetValue WrapName (SettingValue.Toggle value) |> ignore

        [<CLIEvent>]
        member x.SettingChanged = _map.SettingChanged

/// Certain changes need to be synchronized between the editor, local and global 
/// settings.  This MEF component takes care of that synchronization 
[<Export(typeof<IEditorToSettingsSynchronizer>)>]
type internal EditorToSettingSynchronizer
    [<ImportingConstructor>]
    () = 

    let _syncronizingSet = System.Collections.Generic.HashSet<IVimLocalSettings>()
    let _key = obj()

    member x.StartSynchronizing (vimBuffer : IVimBuffer) settingSyncSource = 
        let properties = vimBuffer.TextView.Properties
        if not (properties.ContainsProperty _key) then
            properties.AddProperty(_key, _key)
            x.SetupSynchronization vimBuffer

            match settingSyncSource with
            | SettingSyncSource.Editor -> x.CopyEditorToVimSettings vimBuffer
            | SettingSyncSource.Vim -> x.CopyVimToEditorSettings vimBuffer

    member x.SetupSynchronization (vimBuffer : IVimBuffer) = 
        let editorOptions = vimBuffer.TextView.Options
        if editorOptions <> null then

            let properties = vimBuffer.TextView.Properties
            let bag = DisposableBag()

            // Raised when a local setting is changed.  We need to inspect this setting and 
            // determine if it's an interesting setting and if so synchronize it with the 
            // editor options
            //
            // Cast up to IVimSettings to avoid the F# bug of accessing a CLIEvent from 
            // a derived interface
            let localSettings = vimBuffer.LocalSettings
            (localSettings :> IVimSettings).SettingChanged 
            |> Observable.filter (fun args -> x.IsTrackedLocalSetting args.Setting)
            |> Observable.subscribe (fun _ -> x.CopyVimToEditorSettings vimBuffer)
            |> bag.Add

            // Cast up to IVimSettings to avoid the F# bug of accessing a CLIEvent from 
            // a derived interface
            let windowSettings = vimBuffer.WindowSettings
            (windowSettings :> IVimSettings).SettingChanged
            |> Observable.filter (fun args -> x.IsTrackedWindowSetting args.Setting)
            |> Observable.subscribe (fun _ -> x.CopyVimToEditorSettings vimBuffer)
            |> bag.Add

            /// Raised when an editor option is changed.  If it's one of the values we care about
            /// then we need to sync to the local settings
            editorOptions.OptionChanged
            |> Observable.filter (fun e -> x.IsTrackedEditorSetting e.OptionId)
            |> Observable.subscribe (fun _ -> x.CopyEditorToVimSettings vimBuffer)
            |> bag.Add

            // Finally we need to clean up our listeners when the buffer is closed.  At
            // that point synchronization is no longer needed
            vimBuffer.Closed
            |> Observable.add (fun _ -> 
                properties.RemoveProperty _key |> ignore 
                bag.DisposeAll())

    /// Is this a local setting of note
    member x.IsTrackedLocalSetting (setting : Setting) = 
        if setting.Name = LocalSettingNames.TabStopName then
            true
        elif setting.Name = LocalSettingNames.ShiftWidthName then
            true
        elif setting.Name = LocalSettingNames.ExpandTabName then
            true
        elif setting.Name = LocalSettingNames.NumberName then
            true
        else
            false

    /// Is this a window setting of note
    member x.IsTrackedWindowSetting (setting : Setting) = 
        if setting.Name = WindowSettingNames.CursorLineName then
            true
        elif setting.Name = WindowSettingNames.WrapName then
            true
        else 
            false

    /// Is this an editor setting of note
    member x.IsTrackedEditorSetting optionId =
        if optionId = DefaultOptions.TabSizeOptionId.Name then
            true
        elif optionId = DefaultOptions.IndentSizeOptionId.Name then
            true
        elif optionId = DefaultOptions.ConvertTabsToSpacesOptionId.Name then
            true
        elif optionId = DefaultTextViewHostOptions.LineNumberMarginId.Name then
            true
        elif optionId = DefaultWpfViewOptions.EnableHighlightCurrentLineId.Name then
            true
        elif optionId = DefaultTextViewOptions.WordWrapStyleId.Name then
            true
        else
            false

    /// Synchronize the settings if needed.  Prevent recursive sync's here
    member x.TrySync (vimBuffer : IVimBuffer) syncFunc = 
        let editorOptions = vimBuffer.TextView.Options
        if editorOptions <> null then
            let localSettings = vimBuffer.LocalSettings
            if _syncronizingSet.Add(localSettings) then
                try
                    syncFunc localSettings vimBuffer.WindowSettings editorOptions
                finally
                    _syncronizingSet.Remove(localSettings) |> ignore

    /// Synchronize the settings from the editor to the local settings.  Do not
    /// call this directly but instead call through SynchronizeSettings
    member x.CopyVimToEditorSettings (vimBuffer : IVimBuffer) = 
        x.TrySync vimBuffer (fun localSettings windowSettings editorOptions ->
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.TabSizeOptionId localSettings.TabStop
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.IndentSizeOptionId localSettings.ShiftWidth
            EditorOptionsUtil.SetOptionValue editorOptions DefaultOptions.ConvertTabsToSpacesOptionId localSettings.ExpandTab
            EditorOptionsUtil.SetOptionValue editorOptions DefaultTextViewHostOptions.LineNumberMarginId localSettings.Number
            EditorOptionsUtil.SetOptionValue editorOptions DefaultWpfViewOptions.EnableHighlightCurrentLineId windowSettings.CursorLine

            // Wrap is a difficult option because vim has wrap as on / off while the core editor has
            // 3 different kinds of wrapping.  If we default to only one of them then we will constantly
            // be undoing user settings.  Hence we consider anything but off to be on and hence won't change it 
            let wordWrapStyle = 
                if windowSettings.Wrap then
                    let vimHost = vimBuffer.Vim.VimHost
                    vimHost.GetWordWrapStyle vimBuffer.TextView
                else
                    WordWrapStyles.None
            EditorOptionsUtil.SetOptionValue editorOptions DefaultTextViewOptions.WordWrapStyleId wordWrapStyle)

    /// Synchronize the settings from the local settings to the editor.  Do not
    /// call this directly but instead call through SynchronizeSettings
    member x.CopyEditorToVimSettings (vimBuffer : IVimBuffer) = 
        x.TrySync vimBuffer (fun localSettings windowSettings editorOptions ->
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.TabSizeOptionId with
            | None -> ()
            | Some tabSize -> localSettings.TabStop <- tabSize
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.IndentSizeOptionId with
            | None -> ()
            | Some shiftWidth -> localSettings.ShiftWidth <- shiftWidth
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultOptions.ConvertTabsToSpacesOptionId with
            | None -> ()
            | Some convertTabToSpace -> localSettings.ExpandTab <- convertTabToSpace
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultTextViewOptions.WordWrapStyleId with
            | None -> ()
            | Some wordWrapStyle -> windowSettings.Wrap <- Util.IsFlagSet wordWrapStyle WordWrapStyles.WordWrap
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultTextViewHostOptions.LineNumberMarginId with
            | None -> ()
            | Some show -> localSettings.Number <- show
            match EditorOptionsUtil.GetOptionValue editorOptions DefaultWpfViewOptions.EnableHighlightCurrentLineId with
            | None -> ()
            | Some show -> windowSettings.CursorLine <- show)

    interface IEditorToSettingsSynchronizer with
        member x.StartSynchronizing vimBuffer settingSyncSource = x.StartSynchronizing vimBuffer settingSyncSource
