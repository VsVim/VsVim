#light

namespace Vim

open System.Text.RegularExpressions
open Microsoft.VisualStudio.Text

[<Sealed>]
[<Class>]
type internal ModeLineInterpreter
    (
        textBuffer: ITextBuffer,
        localSettings: IVimLocalSettings
    ) =

    let _textBuffer = textBuffer
    let _localSettings = localSettings

    /// Regular expressions to parse the modeline
    static let _escapedModeLine = @"(([^:\\]|\\:?)*)"
    static let _firstFormRegex = new Regex(@"[ \t]vim:(.*)$", RegexOptions.Compiled)
    static let _secondFormRegex = new Regex(@"[ \t]vim:[ \t]*set[ \t]+" + _escapedModeLine + ":", RegexOptions.Compiled)
    static let _nextGroupRegex = new Regex(_escapedModeLine + @"(:|$)", RegexOptions.Compiled)
    static let _settingPattern = @"([\w[\w\d_]*)"
    static let _assignmentRegex = new Regex(@"^" + _settingPattern + @"=(.*)$", RegexOptions.Compiled)
    static let _settingNameRegex = new Regex(@"^" + _settingPattern + "$", RegexOptions.Compiled)

    let mutable _wasBufferChecked = false

    let _globalSettings = _localSettings.GlobalSettings

    /// List of insecure local setting names that could be used in a file with
    /// a malicious modeline to cause risk or harm to the user
    static let _insecureLocalSettingNames =
        Seq.empty
        |> Seq.toArray
        :> string seq

    /// List of insecure window setting names that could be used in a file with
    /// a malicious modeline to cause risk or harm to the user
    static let _insecureWindowSettingNames =
        Seq.empty
        |> Seq.toArray
        :> string seq

    /// Check the contents of the buffer for a modeline, returning a tuple of
    /// the line we used as a modeline, if any, and a string representing the
    /// first sub-option that produced an error if any
    member x.CheckModeLine (windowSettings: IVimWindowSettings) =

        // Whether we should ignore the setting
        let shouldIgnoreSetting settingName =

            // Ignore empty settings and settings we don't support yet. Ideally
            // we would produce an error for unrecognized settings but vim has
            // many settings and failing on the first unsupported setting would
            // prevent the remainder of the settings from being applied.
            let localSetting = _localSettings.GetSetting settingName
            let windowSetting = windowSettings.GetSetting settingName
            if settingName = "" then

                // Ignore an empty setting.
                true
            elif not (_settingNameRegex.Match(settingName).Success) then

                // Don't ignore illegal setting names.
                false
            elif Option.isNone localSetting && Option.isNone windowSetting then

                // Ignore unsupported settings.
                true
            else
                false

        // Whether we should allow the setting
        let shouldAllowSetting settingName =

            // For security reasons, we disallow certain settings.
            let globalSetting = _globalSettings.GetSetting settingName
            let localSetting = _localSettings.GetSetting settingName
            let windowSetting = windowSettings.GetSetting settingName
            if Option.isSome globalSetting then

                // Disallow all global settings.
                false

            elif Option.isSome localSetting then

                // Allow any local setting that isn't insecure.
                _insecureLocalSettingNames
                |> Seq.contains localSetting.Value.Name
                |> not

            elif Option.isSome  windowSetting then

                // Allow any window setting that isn't insecure.
                _insecureWindowSettingNames
                |> Seq.contains windowSetting.Value.Name
                |> not

            else
                false

        // Set the setting if applicable
        let setSetting settingName settingValue =
            if _localSettings.GetSetting settingName |> Option.isSome then

                // Only set local settings the first time the buffer is
                // checked.
                if not _wasBufferChecked then
                    _localSettings.TrySetValueFromString settingName settingValue
                else
                    true

            elif windowSettings.GetSetting settingName |> Option.isSome then
                windowSettings.TrySetValueFromString settingName settingValue
            else
                false

        // Process a single option like 'ts=8'.
        let processOption (option: string) =
            let option = option.Trim()

            // Determine what kind of option this is.
            let settingName, settingValue =
                let m = _assignmentRegex.Match(option)
                if m.Success then

                    // The option is an assigned setting.
                    let settingName = m.Groups.[1].Value
                    let settingValue = m.Groups.[2].Value
                    settingName, settingValue
                elif option.StartsWith("no") then

                    // The option toggles the setting off.
                    let settingName = option.Substring(2)
                    settingName, "False"
                else

                    // The option toggles the setting on.
                    let settingName = option
                    settingName, "True"

            // Check whether we should apply the setting.
            if shouldIgnoreSetting settingName then
                true
            elif shouldAllowSetting settingName then
                setSetting settingName settingValue
            else
                false

        // Split the options string into fields.
        let splitFields (options: string) =
            options.Replace(@"\:", ":").Split(' ', '\t')

        // Process the "first" format of modeline, i.e. "vim: ...".
        let processFirstForm modeLine =
            let m = _firstFormRegex.Match(modeLine)
            if m.Success then
                let firstBadOption =
                    _nextGroupRegex.Matches(m.Groups.[1].Value)
                    |> Seq.cast<Match>
                    |> Seq.map (fun m -> splitFields m.Groups.[1].Value)
                    |> Seq.concat
                    |> Seq.tryFind (fun option -> not (processOption option))
                Some modeLine, firstBadOption
            else
                None, None

        // Process the "second" format of modeline, i.e. "vim: set ... :".
        let processSecondForm modeLine =
            let m = _secondFormRegex.Match(modeLine)
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
            |> Seq.tryFind (fun (modeLine, _) -> modeLine.IsSome)
            |> Option.defaultValue (None, None)

        // Apply any applicable modelines.
        try
            try
                let modeLines = _globalSettings.ModeLines
                if _globalSettings.ModeLine && modeLines > 0 then
                    tryProcessModeLines modeLines
                else
                    None, None
            with
            | ex ->

                // Empirically, exceptions may be silently caught by some
                // caller in the call stack. As a result, we catch any
                // exceptions here so they are at least reported in the
                // debugger, and so that this can be a convenient place to put
                // a breakpoint.
                VimTrace.TraceError(ex)
                None, None
        finally
            _wasBufferChecked <- true
