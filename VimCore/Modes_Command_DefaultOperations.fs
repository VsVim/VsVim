#light

namespace Vim.Modes.Command
open Vim
open Vim.Modes
open Microsoft.VisualStudio.Text
open Microsoft.VisualStudio.Text.Editor
open Microsoft.VisualStudio.Text.Operations
open Microsoft.VisualStudio.Text.Outlining
open System.Text.RegularExpressions
open Vim.RegexUtil

type internal DefaultOperations ( _data : OperationsData ) =
    inherit CommonOperations(_data)

    let _textView = _data.TextView
    let _operations = _data.EditorOperations;
    let _outlining = _data.OutliningManager;
    let _host = _data.VimHost;
    let _jumpList = _data.JumpList;
    let _settings = _data.LocalSettings;
    let _undoRedoOperations = _data.UndoRedoOperations;
    let _keyMap = _data.KeyMap
    let _statusUtil = _data.StatusUtil
    let _regexFactory = VimRegexFactory(_data.LocalSettings.GlobalSettings)

    /// Format the setting for use in output
    let FormatSetting(setting:Setting) = 

        match setting.Kind,setting.AggregateValue with
        | (ToggleKind,ToggleValue(b)) -> 
            if b then setting.Name
            else sprintf "no%s" setting.Name
        | (StringKind,StringValue(s)) -> sprintf "%s=\"%s\"" setting.Name s
        | (NumberKind,NumberValue(n)) -> sprintf "%s=%d" setting.Name n
        | _ -> "Invalid value"

    member private x.CommonImpl = x :> ICommonOperations

    /// Get the Line spans in the specified range
    member x.GetSpansInRange (range:SnapshotSpan) =
        let startLine = range.Start.GetContainingLine()
        let endLine = range.End.GetContainingLine()
        if startLine.LineNumber = endLine.LineNumber then  Seq.singleton range
        else
            let tss = startLine.Snapshot
            seq {
                yield SnapshotSpan(range.Start, startLine.EndIncludingLineBreak)
                for i = startLine.LineNumber + 1 to endLine.LineNumber - 1 do
                    yield tss.GetLineFromLineNumber(i).ExtentIncludingLineBreak
                yield SnapshotSpan(endLine.Start, range.End)
            }


    interface IOperations with
        member x.EditFile fileName = 
            if not (_host.GoToFile fileName) then _host.Beep()

        member x.ShowOpenFileDialog () = _host.ShowOpenFileDialog()

        /// Implement the :pu[t] command
        member x.Put (text:string) (line:ITextSnapshotLine) isAfter =

            // Force a newline at the end to be consistent with the implemented 
            // behavior in various VIM implementations.  This isn't called out in 
            // the documentation though
            let text = 
                if text.EndsWith(System.Environment.NewLine) then text
                else text + System.Environment.NewLine

            let point = line.Start
            let span =
                if isAfter then x.CommonImpl.PasteAfter point text OperationKind.LineWise
                else x.CommonImpl.PasteBefore point text OperationKind.LineWise

            // Move the cursor to the first non-blank character on the last line
            let line = span.End.Subtract(1).GetContainingLine()
            let rec inner (cur:SnapshotPoint) = 
                if cur = line.End || not (System.Char.IsWhiteSpace(cur.GetChar())) then
                    cur
                else
                    inner (cur.Add(1))
            let point = inner line.Start
            _textView.Caret.MoveTo(point) |> ignore

        member x.Substitute pattern replace (range:SnapshotSpan) flags = 

            /// Actually do the replace with the given regex
            let doReplace (regex:VimRegex) = 
                use edit = _textView.TextBuffer.CreateEdit()

                let replaceOne (span:SnapshotSpan) (c:Capture) = 
                    let newText =  regex.Replace c.Value replace 1
                    let offset = span.Start.Position
                    edit.Replace(Span(c.Index+offset, c.Length), newText) |> ignore
                let getMatches (span:SnapshotSpan) = 
                    if Utils.IsFlagSet flags SubstituteFlags.ReplaceAll then
                        regex.Regex.Matches(span.GetText()) |> Seq.cast<Match>
                    else
                        regex.Regex.Match(span.GetText()) |> Seq.singleton
                let matches = 
                    x.GetSpansInRange range
                    |> Seq.map (fun span -> getMatches span |> Seq.map (fun m -> (m,span)) )
                    |> Seq.concat 
                    |> Seq.filter (fun (m,_) -> m.Success)

                if not (Utils.IsFlagSet flags SubstituteFlags.ReportOnly) then
                    // Actually do the edits
                    matches |> Seq.iter (fun (m,span) -> replaceOne span m)

                let updateReplaceCount () = 
                    // Update the status
                    let replaceCount = matches |> Seq.length
                    let lineCount = 
                        matches 
                        |> Seq.map (fun (_,s) -> s.Start.GetContainingLine().LineNumber)
                        |> Seq.distinct
                        |> Seq.length
                    if replaceCount > 1 then
                        _statusUtil.OnStatus (Resources.CommandMode_SubstituteComplete replaceCount lineCount)

                if edit.HasEffectiveChanges then
                    edit.Apply() |> ignore                                
                    updateReplaceCount()
                elif Utils.IsFlagSet flags SubstituteFlags.ReportOnly then
                    edit.Cancel()
                    updateReplaceCount()
                else 
                    edit.Cancel()
                    _statusUtil.OnError (Resources.CommandMode_PatternNotFound pattern)

            let options = 
                if Utils.IsFlagSet flags SubstituteFlags.IgnoreCase then VimRegexOptions.IgnoreCase
                elif Utils.IsFlagSet flags SubstituteFlags.OrdinalCase then VimRegexOptions.OrdinalCase 
                else VimRegexOptions.None
            let options = VimRegexOptions.Compiled ||| options
            match _regexFactory.CreateWithOptions pattern options with
            | None -> _statusUtil.OnError (Resources.CommandMode_PatternNotFound pattern)
            | Some (regex) -> doReplace regex

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
