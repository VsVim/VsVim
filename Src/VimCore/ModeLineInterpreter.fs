#light

namespace Vim

open System.Text.RegularExpressions
open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type internal ModeLineInterpreter
    (
        _textBuffer: ITextBuffer,
        _localSettings: IVimLocalSettings
    ) =

    /// Regular expressions to parse the modeline
    static let _escapedModeLine = @"(([^:\\]|\\:?)*)";
    static let _firstPattern = @"[ \t]vim:(.*)$"
    static let _secondPattern = @"[ \t]vim:[ \t]*set[ \t]+" + _escapedModeLine + ":"
    static let _nextGroup = _escapedModeLine + @"(:|$)"
    static let _settingPattern = @"([\w[\w\d_]*)";
    static let _assignment = @"^" + _settingPattern + @"=(.*)$"
    static let _settingName = @"^" + _settingPattern + "$"

    let _globalSettings = _localSettings.GlobalSettings

    /// List of insecure local setting names that could be used in a file with
    /// a malicious modeline to cause risk or harm to the user
    static let _insecureLocalSettingNames =
        Seq.empty
        |> Seq.toArray
        :> string seq

    /// Check the contents of the buffer for a modeline
    member x.CheckModeLine () =

        // Whether we should ignore the setting
        let shouldIgnoreSetting settingName =

            // Ignore empty settings and settings we don't support yet. Ideally
            // we would produce an error for unrecognized settings but vim has
            // many settings and failing on the first unsupported setting would
            // prevent the remainder of the settings from being applied.
            let localSetting = _localSettings.GetSetting settingName
            if settingName = "" then

                // Ignore an empty setting.
                true
            elif not (Regex.Match(settingName, _settingName).Success) then

                // Don't ignore illegal setting names.
                false
            elif Option.isNone localSetting then

                // Ignore unsupported settings.
                true
            else
                false

        // Whether we should allow the setting
        let shouldAllowSetting settingName =

            // For security reasons, we disallow certain local settings.
            let globalSetting = _globalSettings.GetSetting settingName
            let localSetting = _localSettings.GetSetting settingName
            if Option.isSome globalSetting then

                // Disallow all global settings.
                false
            elif Option.isSome localSetting then

                // Allow any local setting that isn't insecure.
                _insecureLocalSettingNames
                |> Seq.contains localSetting.Value.Name
                |> not
            else
                false

        // Process a single option like 'ts=8'.
        let processOption (option: string) =
            let option = option.Trim()

            // Determine what kind of option this is.
            let settingName, setter =
                let m = Regex.Match(option, _assignment)
                if m.Success then

                    // The option is an assigned setting.
                    let settingName = m.Groups.[1].Value
                    let strValue = m.Groups.[2].Value
                    let setter = fun () -> _localSettings.TrySetValueFromString settingName strValue
                    settingName, setter
                elif option.StartsWith("no") then

                    // The option toggles the setting off.
                    let settingName = option.Substring(2)
                    let toggleOff = (SettingValue.Toggle false)
                    let setter = fun () ->_localSettings.TrySetValue settingName toggleOff
                    settingName, setter
                else

                    // The option toggles the setting on.
                    let settingName = option
                    let toggleOn = (SettingValue.Toggle true)
                    let setter = fun () -> _localSettings.TrySetValue settingName toggleOn
                    settingName, setter

            // Check whether we should apply the setter.
            if shouldIgnoreSetting settingName then
                true
            elif shouldAllowSetting settingName then
                setter()
            else
                false

        // Split the options string into fields.
        let splitFields (options: string) =
            options.Replace(@"\:", ":").Split(' ', '\t')

        // Process the "first" format of modeline, i.e. "vim: ...".
        let processFirstForm modeLine =
            let m = Regex.Match(modeLine, _firstPattern)
            if m.Success then
                let firstBadOption =
                    Regex.Matches(m.Groups.[1].Value, _nextGroup)
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> splitFields m.Groups.[1].Value)
                    |> Seq.concat
                    |> Seq.tryFind (fun option -> not (processOption option))
                Some modeLine, firstBadOption
            else
                None, None

        // Process the "second" format of modeline, i.e. "vim: set ... :".
        let processSecondForm modeLine =
            let m = Regex.Match(modeLine, _secondPattern)
            if m.Success then
                let firstBadOption =
                    splitFields m.Groups.[1].Value
                    |> Seq.tryFind (fun option -> not (processOption option))
                Some modeLine, firstBadOption
            else
                None, None

        // Try to process either of the two modeline formats.
        let tryProcessModeLine modeLine =
            let result = processSecondForm modeLine
            match result with
            | Some _, _ -> result
            | None, _ ->
               let result = processFirstForm modeLine
               result

        // Try to process the first few and last few lines as modelines.
        let tryProcessModeLines modeLines =
            let lineCount = _textBuffer.CurrentSnapshot.LineCount
            let snapshot = _textBuffer.CurrentSnapshot
            seq {
                yield seq { 0 .. min (modeLines - 1) (lineCount - 1) }
                yield seq { max modeLines (lineCount - modeLines) .. lineCount - 1 }
            }
            |> Seq.concat
            |> Seq.map (SnapshotUtil.GetLine snapshot)
            |> Seq.map SnapshotLineUtil.GetText
            |> Seq.map tryProcessModeLine
            |> SeqUtil.tryFindOrDefault (fun (modeLine, _) -> modeLine.IsSome) (None, None)

        // Apply any applicable modelines.
        try
            let modeLines = _globalSettings.ModeLines
            if _globalSettings.ModeLine && modeLines > 0 then
                tryProcessModeLines modeLines
            else
                None, None
        with
        | ex ->

            // Empirically, exceptions may be silently caught by some caller
            // in the call stack. As a result, we catch any exceptions here so
            // they are at least reported in the debugger, and so that this
            // can be a convenient place to put a breakpoint.
            VimTrace.TraceError("Exception processing the modeline: {0}", ex.Message)
            None, None
