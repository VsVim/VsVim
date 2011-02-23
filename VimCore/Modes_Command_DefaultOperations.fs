#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System.Text.RegularExpressions
open Vim.RegexPatternUtil

type internal DefaultOperations ( _data : OperationsData ) =
    inherit CommonOperations(_data)

    let _textView = _data.TextView
    let _operations = _data.EditorOperations;
    let _outlining = _data.OutliningManager;
    let _vimData = _data.VimData
    let _host = _data.VimHost
    let _jumpList = _data.JumpList;
    let _settings = _data.LocalSettings;
    let _undoRedoOperations = _data.UndoRedoOperations;
    let _keyMap = _data.KeyMap
    let _statusUtil = _data.StatusUtil

    /// Format the setting for use in output
    let FormatSetting(setting:Setting) = 

        match setting.Kind,setting.AggregateValue with
        | (SettingKind.ToggleKind, SettingValue.ToggleValue(b)) -> 
            if b then setting.Name
            else sprintf "no%s" setting.Name
        | (SettingKind.StringKind, SettingValue.StringValue(s)) -> sprintf "%s=\"%s\"" setting.Name s
        | (SettingKind.NumberKind, SettingValue.NumberValue(n)) -> sprintf "%s=%d" setting.Name n
        | _ -> "Invalid value"

    member private x.CommonImpl = x :> ICommonOperations

    interface IOperations with
        member x.ShowOpenFileDialog () = _host.ShowOpenFileDialog()

        /// Implement the :pu[t] command
        member x.Put (text:string) (line:ITextSnapshotLine) isAfter =

            // Force a newline at the end to be consistent with the implemented 
            // behavior in various VIM implementations.  This isn't called out in 
            // the documentation though
            let text = 
                if text.EndsWith(System.Environment.NewLine) then text
                else text + System.Environment.NewLine

            _undoRedoOperations.EditWithUndoTransaction "Paste" (fun () -> 
                let span =
                    let point = if isAfter then line.EndIncludingLineBreak else line.Start
                    x.PutAtWithReturn point (StringData.Simple text) OperationKind.LineWise
                match SnapshotSpanUtil.GetLastIncludedLine span with
                | None -> ()
                | Some(line) ->
                    let point = SnapshotLineUtil.GetIndent line
                    TextViewUtil.MoveCaretToPoint _textView point)

        member x.PrintMarks (markMap:IMarkMap) =    
            let printMark (ident:char) (point:VirtualSnapshotPoint) =
                let textLine = point.Position.GetContainingLine()
                let lineNum = textLine.LineNumber
                let column = point.Position.Position - textLine.Start.Position
                let column = if point.IsInVirtualSpace then column + point.VirtualSpaces else column
                let name = _host.GetName _textView.TextBuffer
                sprintf " %c   %5d%5d%s" ident lineNum column name

            let localSeq = markMap.GetLocalMarks _textView.TextBuffer |> Seq.sortBy (fun (c,_) -> c)
            let globalSeq = markMap.GetGlobalMarks() |> Seq.sortBy (fun (c,_) -> c)
            localSeq 
            |> Seq.append globalSeq
            |> Seq.map (fun (c,p) -> printMark c p )
            |> Seq.append ( "mark line  col file/text"  |> Seq.singleton)
            |> _statusUtil.OnStatusLong

        member x.PrintModifiedSettings () = 
            _settings.AllSettings |> Seq.filter (fun s -> not s.IsValueDefault) |> Seq.map FormatSetting |> _statusUtil.OnStatusLong

        member x.PrintAllSettings () = 
            _settings.AllSettings |> Seq.map FormatSetting |> _statusUtil.OnStatusLong
            
        member x.PrintSetting settingName = 
            match _settings.GetSetting settingName with 
            | None -> _statusUtil.OnError (Resources.CommandMode_UnknownOption settingName)
            | Some(setting) -> setting |> FormatSetting |> _statusUtil.OnStatus

        member x.OperateSetting settingName = 
            match _settings.GetSetting settingName with
            | None -> _statusUtil.OnError (Resources.CommandMode_UnknownOption settingName)
            | Some(setting) ->
                if setting.Kind = ToggleKind then _settings.TrySetValue settingName (ToggleValue(true)) |> ignore
                else setting |> FormatSetting |> _statusUtil.OnStatus

        member x.ResetSetting settingName =
            match _settings.GetSetting settingName with
            | None -> _statusUtil.OnError (Resources.CommandMode_UnknownOption settingName)
            | Some(setting) ->
                if setting.Kind = ToggleKind then _settings.TrySetValue settingName (ToggleValue(false)) |> ignore
                else settingName |> Resources.CommandMode_InvalidArgument |> _statusUtil.OnError
            
        member x.InvertSetting settingName = 
            match _settings.GetSetting settingName with
            | None -> _statusUtil.OnError (Resources.CommandMode_UnknownOption settingName)
            | Some(setting) ->
                match setting.Kind,setting.AggregateValue with
                | (ToggleKind,ToggleValue(b)) -> _settings.TrySetValue settingName (ToggleValue(not b)) |> ignore
                | _ -> settingName |> Resources.CommandMode_InvalidArgument |> _statusUtil.OnError

        member x.SetSettingValue settingName value = 
            let ret = _settings.TrySetValueFromString settingName value 
            if not ret then 
                Resources.CommandMode_InvalidValue settingName value |> _statusUtil.OnError

        member x.RemapKeys (lhs:string) (rhs:string) (modes:KeyRemapMode seq) allowRemap = 
            let func = 
                if allowRemap then _keyMap.MapWithRemap
                else _keyMap.MapWithNoRemap
            let failed = 
                modes
                |> Seq.map (fun mode -> (mode,func lhs rhs mode))
                |> Seq.filter (fun (_,ret) -> not ret)
            if not (failed |> Seq.isEmpty) then
                _statusUtil.OnError (Resources.CommandMode_NotSupported_KeyMapping lhs rhs)

        member x.ClearKeyMapModes modes = modes |> Seq.iter (fun mode -> _keyMap.Clear mode)

        member x.UnmapKeys lhs modes = 
            let allSucceeded =
                modes 
                |> Seq.map (fun mode -> _keyMap.Unmap lhs mode)
                |> Seq.filter (fun x -> not x)
                |> Seq.isEmpty
            if not allSucceeded then _statusUtil.OnError Resources.CommandMode_NoSuchMapping

        // :help map-listing
        member x.PrintKeyMap modes =

            // Get the printable info for the set of modes
            let getModeLine modes =
                if ListUtil.contains KeyRemapMode.Normal modes 
                    && ListUtil.contains KeyRemapMode.OperatorPending modes
                    && ListUtil.contains KeyRemapMode.Visual modes then
                    " "
                elif ListUtil.contains KeyRemapMode.Command modes 
                    && ListUtil.contains KeyRemapMode.Insert modes then
                    "!"
                elif ListUtil.contains KeyRemapMode.Visual modes 
                    && ListUtil.contains KeyRemapMode.Select modes then
                    "v"
                elif List.length modes <> 1 then 
                    "?"
                else 
                    match List.head modes with
                    | KeyRemapMode.Normal -> "n"
                    | KeyRemapMode.Visual -> "x"
                    | KeyRemapMode.Select -> "s"
                    | KeyRemapMode.OperatorPending -> "o"
                    | KeyRemapMode.Command -> "c"
                    | KeyRemapMode.Language -> "l"
                    | KeyRemapMode.Insert -> "i"

            // Get the printable format for the KeyInputSet 
            let getKeyInputSetLine (keyInputSet:KeyInputSet) = 

                let inner (ki:KeyInput) = 

                    let ki = ki |> KeyInputUtil.GetAlternateTarget |> OptionUtil.getOrDefault ki

                    // Build up the prefix for the specified modifiers
                    let rec getPrefix modifiers = 
                        if Util.IsFlagSet modifiers KeyModifiers.Alt then
                            "M-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Alt)
                        elif Util.IsFlagSet modifiers KeyModifiers.Control then
                            "C-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Control)
                        elif Util.IsFlagSet modifiers KeyModifiers.Shift then
                            "S-" + getPrefix (Util.UnsetFlag modifiers KeyModifiers.Shift)
                        else 
                            ""

                    // Get the actual printable output for the raw KeyInput.  For a KeyInput with
                    // a char this is straight forward.  Non-char KeyInput need to be special cased
                    // though
                    let prefix,output = 
                        match (KeyNotationUtil.TryGetSpecialKeyName ki),ki.Char with
                        | Some(name,extraModifiers), _ -> 
                            (getPrefix extraModifiers, name)
                        | None, c -> 
                            let c = 
                                if CharUtil.IsLetter c && ki.KeyModifiers <> KeyModifiers.None then CharUtil.ToUpper c 
                                else c
                            (getPrefix ki.KeyModifiers, StringUtil.ofChar c)

                    if String.length prefix = 0 then 
                        if String.length output = 1 then output
                        else sprintf "<%s>" output
                    else
                        sprintf "<%s%s>" prefix output 

                keyInputSet.KeyInputs |> Seq.map inner |> String.concat ""

            // Get the printable line for the provided mode, left and right side
            let getLine modes lhs rhs = 
                sprintf "%-5s%s %s" (getModeLine modes) (getKeyInputSetLine lhs) (getKeyInputSetLine rhs)

            let lines = 
                modes
                |> Seq.map (fun mode -> 
                    mode
                    |> _keyMap.GetKeyMappingsForMode 
                    |> Seq.map (fun (lhs,rhs) -> (mode,lhs,rhs)))
                |> Seq.concat
                |> Seq.groupBy (fun (mode,lhs,rhs) -> lhs)
                |> Seq.map (fun (lhs, all) ->
                    let modes = all |> Seq.map (fun (mode, _, _) -> mode) |> List.ofSeq
                    let rhs = all |> Seq.map (fun (_, _, rhs) -> rhs) |> Seq.head
                    getLine modes lhs rhs)

            _statusUtil.OnStatusLong lines
